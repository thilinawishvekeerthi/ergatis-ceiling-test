namespace Talent.Security.Contracts;

/// <summary>Role codes in the default permission matrix. Roles are data: a client's matrix may add others.</summary>
public static class TalentRoles
{
    public const string Employee = "EMPLOYEE";
    public const string Manager = "MANAGER";
    public const string HrAdmin = "HR_ADMIN";
    public const string TalentCommittee = "TALENT_COMMITTEE";
}
