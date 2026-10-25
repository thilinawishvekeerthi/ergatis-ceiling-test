using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Talent.Security.Http;

/// <summary>
/// The deny-by-default fallback policy also reaches requests that matched no endpoint, which would answer 401 for a route
/// that does not exist. Those requests get the normal 404 instead. Every real endpoint still needs a signed-in user.
/// </summary>
internal sealed class UnknownRouteAwareAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Succeeded && context.GetEndpoint() is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
