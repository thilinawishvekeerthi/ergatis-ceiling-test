using System.Security.Claims;

namespace Talent.Security.Contracts;

/// <summary>Plug point: where roles come from (TalentManaged tables or IdP groups). Chosen by the setting <c>Security:RoleSource</c>.</summary>
public interface IRoleSource
{
    Task<IReadOnlySet<string>> GetRolesAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

/// <summary>Plug point: maps a login subject to an employee. Today a Security table, later ExternalIdMap (system SSO).</summary>
public interface IEmployeeResolver
{
    Task<Guid?> ResolveAsync(string subject, CancellationToken cancellationToken);
}
