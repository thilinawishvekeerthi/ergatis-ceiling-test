namespace Talent.Security.Contracts;

/// <summary>What a caller wants to do with an object (ADR-07). Export is its own action and is always audited.</summary>
public enum PermissionAction
{
    View = 0,
    Edit = 1,
    Approve = 2,
    Calibrate = 3,
    Export = 4,
}
