using Talent.Security.Contracts;
using Talent.Security.Scopes;

namespace Talent.Security.Access;

/// <summary>ADR-07 check order. Callers get 401 for no session, 403 for everything else that fails.</summary>
internal sealed class AccessGuard(ISecurityUser user, PermissionMatrixProvider matrices, DataScopeService scopes) : IAccessGuard
{
    public async Task<AccessGrant> RequireAsync(
        string resource, PermissionAction action, Guid? subjectEmployeeId, CancellationToken cancellationToken)
    {
        var employee = RequireEmployee();
        var matrix = await matrices.GetAsync(cancellationToken);

        var grant = matrix.Evaluate(user.Roles, resource, action)
                    ?? throw new ForbiddenException("You do not have permission to do this.");

        var inScope = subjectEmployeeId is { } subject
            ? await scopes.ContainsAsync(grant, subject, cancellationToken)
            : grant.CoversAllEmployees; // organisation-wide objects need the All scope
        if (!inScope)
        {
            throw new ForbiddenException("This is outside your data scope.");
        }

        ConflictOfInterestGuard.Ensure(action, employee, subjectEmployeeId);
        return grant;
    }

    public async Task RequireAnyAsync(string resourcePrefix, PermissionAction action, CancellationToken cancellationToken)
    {
        RequireEmployee();
        var matrix = await matrices.GetAsync(cancellationToken);
        if (!matrix.HasAny(user.Roles, resourcePrefix, action))
        {
            throw new ForbiddenException("You do not have permission to do this.");
        }
    }

    private Guid RequireEmployee()
    {
        if (!user.IsAuthenticated)
        {
            throw new NotAuthenticatedException();
        }

        return user.EmployeeId ?? throw new ForbiddenException("Your login is not linked to an employee.");
    }
}
