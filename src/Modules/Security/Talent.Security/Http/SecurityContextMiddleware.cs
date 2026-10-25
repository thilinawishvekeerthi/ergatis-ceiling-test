using Talent.Security.Contracts;
using Talent.Security.Roles;

namespace Talent.Security.Http;

/// <summary>Builds the current user once per request: roles through <see cref="IRoleSource"/>, employee through <see cref="IEmployeeResolver"/>.</summary>
internal sealed class SecurityContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SecurityContext current, IRoleSource roles, IEmployeeResolver employees)
    {
        if (context.User.Identity?.IsAuthenticated == true && Subjects.Of(context.User) is { } subject)
        {
            var ct = context.RequestAborted;
            current.SignIn(subject, await employees.ResolveAsync(subject, ct), await roles.GetRolesAsync(context.User, ct));
        }

        await next(context);
    }
}
