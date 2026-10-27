using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Talent.Config.Contracts;
using Talent.Security.Access;
using Talent.Security.Contracts;
using Talent.Security.Http;
using Talent.Security.Masking;
using Talent.Security.Matrix;
using Talent.Security.Persistence;
using Talent.Security.Roles;
using Talent.Security.Scopes;
using Talent.Security.Sessions;
using Talent.Shared;
using Talent.Shared.Security;

namespace Talent.Security;

/// <summary>The only public entry point of the Security module (ADR-07).</summary>
public static class SecurityModule
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(SharedServiceCollectionExtensions.ConnectionStringName)
                         ?? throw new InvalidOperationException("Connection string 'Talent' is missing.");

        var early = new SecurityOptions();
        Bind(configuration, early);

        services.AddOptions<SecurityOptions>().Configure(o => Bind(configuration, o)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();

        services.AddDbContext<SecurityDbContext>(o =>
            SecurityDbContextFactory.Configure((DbContextOptionsBuilder<SecurityDbContext>)o, connection));

        // The login subject becomes ICurrentUser for the whole request. This replaces the anonymous default from Talent.Shared.
        services.RemoveAll<ICurrentUser>();
        services.AddScoped<SecurityContext>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<SecurityContext>());
        services.AddScoped<ISecurityUser>(sp => sp.GetRequiredService<SecurityContext>());

        services.AddScoped<IRoleSource>(sp => sp.GetRequiredService<IOptions<SecurityOptions>>().Value.RoleSource switch
        {
            RoleSourceKind.IdpGroups => ActivatorUtilities.CreateInstance<IdpGroupsRoleSource>(sp),
            _ => ActivatorUtilities.CreateInstance<TalentManagedRoleSource>(sp),
        });
        services.AddScoped<IEmployeeResolver, AccountEmployeeResolver>();

        services.TryAddScoped<IScopeMemberSource, EmptyScopeMemberSource>();
        services.AddScoped<DataScopeService>();
        services.AddScoped<IDataScopeService>(sp => sp.GetRequiredService<DataScopeService>());
        services.AddScoped<PermissionMatrixProvider>();
        services.AddScoped<IAccessGuard, AccessGuard>();
        services.AddSingleton<IFieldMasker, FieldMasker>();
        services.AddSingleton<IConfigPayloadValidator, PermissionMatrixPayloadValidator>();

        services.AddExceptionHandler<ProblemExceptionHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, UnknownRouteAwareAuthorizationHandler>();
        services.AddAntiforgery(o =>
        {
            o.HeaderName = SessionCookieSettings.AntiforgeryHeader;
            o.Cookie.Name = SessionCookieSettings.AntiforgeryName;
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.SecurePolicy = early.Cookie.RequireSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        });

        // Deny by default: an endpoint without its own rule still needs a signed-in user.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        var auth = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, o =>
            {
                o.Cookie.Name = SessionCookieSettings.Name;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax; // Lax so the redirect back from the identity provider carries the cookie
                o.Cookie.SecurePolicy = early.Cookie.RequireSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromMinutes(early.Cookie.LifetimeMinutes);
                o.SlidingExpiration = true;

                // An API answers 401 and 403. It never redirects to a login page.
                o.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                o.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        if (early.Mode == AuthenticationMode.Oidc)
        {
            auth.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, _ => { });
            services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
                .Configure<IOptions<SecurityOptions>>((o, security) =>
                {
                    var oidc = security.Value.Oidc;
                    o.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    o.Authority = oidc.Authority;
                    o.ClientId = oidc.ClientId;
                    o.ClientSecret = oidc.ClientSecret;
                    o.ResponseType = OpenIdConnectResponseType.Code;
                    o.UsePkce = true;
                    o.SaveTokens = false; // tokens stay out of the cookie: the browser never holds them
                    o.GetClaimsFromUserInfoEndpoint = true;
                    o.MapInboundClaims = false; // keep "sub" and "groups" as the identity provider sends them
                    o.Scope.Clear();
                    o.Scope.Add("openid");
                    o.Scope.Add("profile");
                });
        }

        return services;
    }

    /// <summary>Applies the Security migrations, publishes the default permission matrix, and seeds development users.</summary>
    public static async Task InitializeSecurityAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        await scope.ServiceProvider.GetRequiredService<IConfigDefaultPublisher>().EnsurePublishedAsync(
            ConfigType.PermissionMatrix, PermissionMatrix.ConfigCode, DefaultPermissionMatrix.Name, DefaultPermissionMatrix.Json, cancellationToken);

        var seed = scope.ServiceProvider.GetRequiredService<IOptions<SecurityOptions>>().Value;
        if (seed.Mode == AuthenticationMode.Fake)
        {
            await SeedUsersAsync(db, seed.Seed, cancellationToken);
        }
    }

    /// <summary>Authentication, the current user, the anti-forgery check and authorization, in that order.</summary>
    public static IApplicationBuilder UseTalentSecurity(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseAuthentication();
        app.UseMiddleware<SecurityContextMiddleware>();
        app.UseMiddleware<AntiforgeryGuardMiddleware>();
        app.UseAuthorization();
        return app;
    }

    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        BffEndpoints.Map(app, app.ServiceProvider.GetRequiredService<IOptions<SecurityOptions>>().Value);
        return app;
    }

    private static void Bind(IConfiguration configuration, SecurityOptions options)
    {
        configuration.GetSection("Security").Bind(options);
        configuration.GetSection("Authentication").Bind(options);
    }

    private static async Task SeedUsersAsync(SecurityDbContext db, List<SeedUser> users, CancellationToken cancellationToken)
    {
        foreach (var user in users.Where(u => !string.IsNullOrWhiteSpace(u.Subject)))
        {
            if (!await db.Accounts.AnyAsync(a => a.Subject == user.Subject, cancellationToken))
            {
                db.Accounts.Add(new UserAccount(user.Subject, user.EmployeeId));
            }

            foreach (var role in user.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
            {
                if (!await db.RoleAssignments.AnyAsync(r => r.Subject == user.Subject && r.Role == role, cancellationToken))
                {
                    db.RoleAssignments.Add(new RoleAssignment(user.Subject, role));
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
