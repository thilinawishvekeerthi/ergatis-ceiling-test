namespace Talent.Security.Contracts;

/// <summary>
/// The check order of ADR-07 for the service layer: role allows the action (403), record in scope (403),
/// not the caller's own record for Edit, Approve and Calibrate (403). Session checks (401) happen before.
/// </summary>
public interface IAccessGuard
{
    /// <param name="resource">Object name such as <c>config:Scale</c>.</param>
    /// <param name="subjectEmployeeId">The employee the record is about. Null for organisation-wide objects, which need scope All.</param>
    Task<AccessGrant> RequireAsync(string resource, PermissionAction action, Guid? subjectEmployeeId, CancellationToken cancellationToken);

    /// <summary>Passes when the caller may do <paramref name="action"/> on at least one object whose name starts with <paramref name="resourcePrefix"/>.</summary>
    Task RequireAnyAsync(string resourcePrefix, PermissionAction action, CancellationToken cancellationToken);
}
