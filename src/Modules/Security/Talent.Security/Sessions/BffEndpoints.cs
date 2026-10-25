using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Talent.Security.Contracts;
using Talent.Shared.Errors;

namespace Talent.Security.Sessions;

/// <summary>The BFF surface for the React app. Tokens stay on the server: the browser only gets the session cookie.</summary>
internal static class BffEndpoints
{
    private sealed record FakeLoginRequest(string? Subject);

    public static void Map(IEndpointRouteBuilder app, SecurityOptions options)
    {
        var bff = app.MapGroup("/bff");

        bff.MapGet("/user", (ISecurityUser user) => Results.Ok(new
        {
            userId = user.UserId,
            roles = user.Roles.Order(StringComparer.Ordinal),
            hasEmployee = user.EmployeeId is not null,
        })).RequireAuthorization();

        bff.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { headerName = tokens.HeaderName, requestToken = tokens.RequestToken });
        }).RequireAuthorization();

        if (options.Mode == AuthenticationMode.Oidc)
        {
            bff.MapGet("/login", (string? returnUrl) => Results.Challenge(
                new AuthenticationProperties { RedirectUri = LocalUrl(returnUrl) },
                [OpenIdConnectDefaults.AuthenticationScheme])).AllowAnonymous();

            bff.MapPost("/logout", () => Results.SignOut(
                new AuthenticationProperties { RedirectUri = "/" },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme])).RequireAuthorization();
        }
        else
        {
            bff.MapPost("/fake-login", async (FakeLoginRequest request, HttpContext context) =>
            {
                var subject = request.Subject?.Trim();
                if (string.IsNullOrEmpty(subject) || subject.Length > 200)
                {
                    throw new ValidationFailedException(new Dictionary<string, string[]> { ["subject"] = ["A subject of 1 to 200 characters is required."] });
                }

                var identity = new ClaimsIdentity([new Claim("sub", subject)], CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                return Results.NoContent();
            }).AllowAnonymous();

            bff.MapPost("/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.NoContent();
            }).RequireAuthorization();
        }
    }

    /// <summary>Only a path on this site. Anything else would be an open redirect.</summary>
    private static string LocalUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl[0] == '/' && (returnUrl.Length == 1 || returnUrl[1] is not ('/' or '\\'))
            ? returnUrl
            : "/";
}
