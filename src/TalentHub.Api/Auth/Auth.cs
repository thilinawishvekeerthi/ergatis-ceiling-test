using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using TalentHub.Api.Data;
using TalentHub.Api.Domain;

namespace TalentHub.Api.Auth;

public static class AuthSetup
{
    public const string Bearer = "Bearer";

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration cfg)
    {
        services
            .AddIdentityCore<Employee>(o => { o.User.RequireUniqueEmail = false; })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TalentHubDbContext>()
            .AddSignInManager();

        var jwt = cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key missing")));
        services.AddAuthentication(Bearer)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    IssuerSigningKey = key
                };
            });
        services.AddAuthorization(o =>
        {
            foreach (AppRole r in Enum.GetValues<AppRole>())
                o.AddPolicy(r.ToString(), p => p.RequireRole(r.ToString()));
        });
        return services;
    }

    public static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var rm = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (AppRole r in Enum.GetValues<AppRole>())
            if (!await rm.RoleExistsAsync(r.ToString()))
                await rm.CreateAsync(new IdentityRole(r.ToString()));
    }
}

public record LoginRequest(string Username, string Password);

public static class AuthEndpoints
{
    public static IEndpointConventionBuilder MapAuthEndpoints(this IEndpointRouteBuilder app) => app.MapPost("/api/auth/login",
        async (LoginRequest req, UserManager<Employee> um, SignInManager<Employee> sim, IConfiguration cfg) =>
        {
            var user = await um.FindByNameAsync(req.Username);
            var result = user is null ? null : await sim.CheckPasswordSignInAsync(user, req.Password, false);
            Console.WriteLine($"LOGIN {req.Username}: found={user is not null} active={user?.IsActive} result={result}");
            if (user is null || !user.IsActive || !(result?.Succeeded ?? false))
                return Results.Unauthorized();

            var roles = await um.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName!),
                new(ClaimTypes.GivenName, user.FullName)
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var jwt = cfg.GetSection("Jwt");
            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.TryParse(jwt["ExpiryMinutes"], out var m) ? m : 480),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)), SecurityAlgorithms.HmacSha256));
            return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), username = user.UserName, fullName = user.FullName, roles });
        });
}
