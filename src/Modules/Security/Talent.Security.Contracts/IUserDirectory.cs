namespace Talent.Security.Contracts;

/// <summary>
/// Who the login users are, for modules that must route work to people (for example Workflow approvals).
/// It answers from Security's own tables. With the IdP groups role source, role members are not known to Talent
/// and <see cref="GetSubjectsInRoleAsync"/> returns nobody.
/// </summary>
public interface IUserDirectory
{
    /// <summary>The login subject mapped to an employee, or null when that employee has no login.</summary>
    Task<string?> FindSubjectAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>Login subjects that hold a role, in a stable order.</summary>
    Task<IReadOnlyList<string>> GetSubjectsInRoleAsync(string role, CancellationToken cancellationToken);
}
