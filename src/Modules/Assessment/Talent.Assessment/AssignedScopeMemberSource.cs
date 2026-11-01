using Microsoft.EntityFrameworkCore;
using Talent.Assessment.Persistence;
using Talent.Security.Contracts;

namespace Talent.Assessment;

/// <summary>
/// Supplies the CycleAssigned scope: the employees the caller has been assigned to review. Every other scope goes to the
/// member source that was registered before this one (Integration later supplies DirectReports and DeptTree).
/// </summary>
internal sealed class AssignedScopeMemberSource(AssessmentDbContext db, IScopeMemberSource inner) : IScopeMemberSource
{
    public async Task<IReadOnlyCollection<Guid>> GetEmployeeIdsAsync(DataScope scope, Guid callerEmployeeId, CancellationToken cancellationToken) =>
        scope == DataScope.CycleAssigned
            ? await db.Reviews.Where(r => r.ReviewerId == callerEmployeeId).Select(r => r.EmployeeId).Distinct().ToListAsync(cancellationToken)
            : await inner.GetEmployeeIdsAsync(scope, callerEmployeeId, cancellationToken);
}
