using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Talent.Security.Sessions;

internal enum AuthenticationMode
{
    /// <summary>The client's identity provider through the OpenID Connect handler (authorization code + PKCE).</summary>
    Oidc = 0,

    /// <summary>A fake login endpoint for tests and local development. Refused in Production.</summary>
    Fake = 1,
}

internal enum RoleSourceKind
{
    TalentManaged = 0,
    IdpGroups = 1,
}

/// <summary>Bound from <c>Authentication</c> (Mode, Oidc) and <c>Security</c> (RoleSource, Cookie, GroupRoleMap, Seed). No secrets in appsettings.</summary>
internal sealed class SecurityOptions
{
    public AuthenticationMode Mode { get; set; } = AuthenticationMode.Oidc;

    public RoleSourceKind RoleSource { get; set; } = RoleSourceKind.TalentManaged;

    public OidcSettings Oidc { get; set; } = new();

    public SessionCookieSettings Cookie { get; set; } = new();

    /// <summary>For IdpGroups: IdP group value to Talent role code.</summary>
    public Dictionary<string, string> GroupRoleMap { get; set; } = [];

    public string GroupClaim { get; set; } = "groups";

    /// <summary>Users created at startup when migrations run on startup (local development only).</summary>
    public List<SeedUser> Seed { get; set; } = [];
}

internal sealed class OidcSettings
{
    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    /// <summary>From user secrets or the environment, never from appsettings.</summary>
    public string? ClientSecret { get; set; }
}

internal sealed class SessionCookieSettings
{
    public const string Name = "Talent.Session";
    public const string AntiforgeryName = "Talent.Xsrf";
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";

    /// <summary>Only tests and plain-http local development turn this off.</summary>
    public bool RequireSecure { get; set; } = true;

    public int LifetimeMinutes { get; set; } = 480;
}

internal sealed class SeedUser
{
    public string Subject { get; set; } = "";

    public Guid EmployeeId { get; set; }

    public List<string> Roles { get; set; } = [];
}

internal sealed class SecurityOptionsValidator(IHostEnvironment environment) : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();
        var production = environment.IsProduction();

        if (options.Mode == AuthenticationMode.Fake && production)
        {
            failures.Add("Authentication:Mode Fake is for tests and development. It cannot run in Production.");
        }

        if (options.Mode == AuthenticationMode.Oidc)
        {
            if (string.IsNullOrWhiteSpace(options.Oidc.Authority))
            {
                failures.Add("Authentication:Oidc:Authority is required.");
            }
            else if (!Uri.TryCreate(options.Oidc.Authority, UriKind.Absolute, out var authority)
                     || authority.Scheme != Uri.UriSchemeHttps && !environment.IsDevelopment())
            {
                failures.Add("Authentication:Oidc:Authority must be an https URL.");
            }

            if (string.IsNullOrWhiteSpace(options.Oidc.ClientId))
            {
                failures.Add("Authentication:Oidc:ClientId is required.");
            }
        }

        if (production && !options.Cookie.RequireSecure)
        {
            failures.Add("Security:Cookie:RequireSecure cannot be false in Production.");
        }

        if (options.Cookie.LifetimeMinutes is < 5 or > 1440)
        {
            failures.Add("Security:Cookie:LifetimeMinutes must be between 5 and 1440.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
