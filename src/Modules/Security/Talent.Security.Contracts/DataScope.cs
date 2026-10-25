namespace Talent.Security.Contracts;

/// <summary>Which employees' records a grant reaches (ADR-07).</summary>
public enum DataScope
{
    Self = 0,
    DirectReports = 1,
    DeptTree = 2,
    CycleAssigned = 3,
    All = 4,
}
