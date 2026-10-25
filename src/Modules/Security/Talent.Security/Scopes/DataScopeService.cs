using System.Linq.Expressions;
using Talent.Security.Contracts;

namespace Talent.Security.Scopes;

/// <summary>
/// Builds the scope filter that goes into the SQL query (ADR-07). The caller's scopes are combined as a union.
/// A caller with no employee, or a scope nobody supplies members for, reaches nobody.
/// </summary>
internal sealed class DataScopeService(ISecurityUser user, IScopeMemberSource members) : IDataScopeService
{
    public async Task<Expression<Func<T, bool>>> FilterAsync<T>(
        AccessGrant grant, Expression<Func<T, Guid>> employeeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(employeeId);

        if (grant.CoversAllEmployees)
        {
            return _ => true;
        }

        var ids = await ResolveAsync(grant, cancellationToken);
        if (ids.Length == 0)
        {
            return _ => false;
        }

        var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid)], Expression.Constant(ids), employeeId.Body);
        return Expression.Lambda<Func<T, bool>>(contains, employeeId.Parameters);
    }

    public async Task<bool> ContainsAsync(AccessGrant grant, Guid employeeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return grant.CoversAllEmployees || (await ResolveAsync(grant, cancellationToken)).Contains(employeeId);
    }

    private async Task<Guid[]> ResolveAsync(AccessGrant grant, CancellationToken cancellationToken)
    {
        if (user.EmployeeId is not { } me)
        {
            return [];
        }

        var ids = new HashSet<Guid>();
        foreach (var scope in grant.Scopes)
        {
            switch (scope)
            {
                case DataScope.Self:
                    ids.Add(me);
                    break;
                case DataScope.All:
                    break;
                default:
                    ids.UnionWith(await members.GetEmployeeIdsAsync(scope, me, cancellationToken));
                    break;
            }
        }

        return [.. ids];
    }
}

/// <summary>Default member source: reaches nobody until Integration (org tree) and Workflow (cycle assignments) supply theirs.</summary>
internal sealed class EmptyScopeMemberSource : IScopeMemberSource
{
    public Task<IReadOnlyCollection<Guid>> GetEmployeeIdsAsync(DataScope scope, Guid callerEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<Guid>>([]);
}
