using Talent.Shared.Security;

namespace Talent.Security.Contracts;

/// <summary>The signed-in caller with roles and employee, built once per request.</summary>
public interface ISecurityUser : ICurrentUser
{
    /// <summary>The employee this login maps to. Null means no mapped employee, and every check fails with 403.</summary>
    Guid? EmployeeId { get; }

    IReadOnlySet<string> Roles { get; }
}
