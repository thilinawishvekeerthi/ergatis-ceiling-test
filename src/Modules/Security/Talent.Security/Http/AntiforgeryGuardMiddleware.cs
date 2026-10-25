using Microsoft.AspNetCore.Antiforgery;
using Talent.Shared.Errors;

namespace Talent.Security.Http;

/// <summary>
/// The browser sends the session cookie by itself, so every state-changing call from a signed-in user needs the anti-forgery header.
/// Calls without a session are not checked here: they end as 401 further down.
/// </summary>
internal sealed class AntiforgeryGuardMiddleware(RequestDelegate next)
{
    private static readonly string[] Exempt = ["/bff/fake-login", "/bff/login"];

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method)
            && !HttpMethods.IsTrace(context.Request.Method)
            && context.User.Identity?.IsAuthenticated == true
            && !Exempt.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase)
            && !await antiforgery.IsRequestValidAsync(context))
        {
            throw new AntiforgeryRejectedException();
        }

        await next(context);
    }
}

internal sealed class AntiforgeryRejectedException()
    : TalentProblemException(400, "The anti-forgery token is missing or invalid.");
