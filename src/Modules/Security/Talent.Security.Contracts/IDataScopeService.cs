using System.Linq.Expressions;

namespace Talent.Security.Contracts;

/// <summary>Turns a grant's data scopes into an EF Core predicate, so the filter runs in SQL and not after loading rows.</summary>
public interface IDataScopeService
{
    Task<Expression<Func<T, bool>>> FilterAsync<T>(
        AccessGrant grant, Expression<Func<T, Guid>> employeeId, CancellationToken cancellationToken);
}

/// <summary>
/// Plug point: the employees a scope reaches for the caller. Self and All are built in.
/// DirectReports and DeptTree come from the Integration module, CycleAssigned from Workflow.
/// Until a module supplies one, those scopes reach nobody (deny by default).
/// </summary>
public interface IScopeMemberSource
{
    Task<IReadOnlyCollection<Guid>> GetEmployeeIdsAsync(DataScope scope, Guid callerEmployeeId, CancellationToken cancellationToken);
}
