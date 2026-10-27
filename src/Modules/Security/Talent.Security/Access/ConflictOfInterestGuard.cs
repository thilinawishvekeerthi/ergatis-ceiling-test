using Talent.Security.Contracts;

namespace Talent.Security.Access;

/// <summary>An employee never edits, approves or calibrates their own record (ADR-07, check order step 4).</summary>
internal static class ConflictOfInterestGuard
{
    public static void Ensure(PermissionAction action, Guid callerEmployeeId, Guid? subjectEmployeeId)
    {
        if (action is PermissionAction.Edit or PermissionAction.Approve or PermissionAction.Calibrate
            && subjectEmployeeId == callerEmployeeId)
        {
            throw new ForbiddenException("You cannot do this on your own record.");
        }
    }
}
