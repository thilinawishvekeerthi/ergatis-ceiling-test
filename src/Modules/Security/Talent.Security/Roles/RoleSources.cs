using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Talent.Security.Contracts;
using Talent.Security.Persistence;
using Talent.Security.Sessions;

namespace Talent.Security.Roles;

internal static class Subjects
{
    public static string? Of(ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
}

/// <summary>Roles that Talent keeps itself, in the <c>security.role_assignments</c> table (the first role source).</summary>
internal sealed class TalentManagedRoleSource(SecurityDbContext db) : IRoleSource
{
    public async Task<IReadOnlySet<string>> GetRolesAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (Subjects.Of(principal) is not { } subject)
        {
            return new HashSet<string>();
        }

        var roles = await db.RoleAssignments.AsNoTracking()
            .Where(r => r.Subject == subject)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);
        return roles.ToHashSet();
    }
}

/// <summary>Roles from the identity provider's group claim, mapped to Talent role codes. Unmapped groups give no role.</summary>
internal sealed class IdpGroupsRoleSource(IOptions<SecurityOptions> options) : IRoleSource
{
    public Task<IReadOnlySet<string>> GetRolesAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var settings = options.Value;
        var roles = principal.FindAll(settings.GroupClaim)
            .Select(c => settings.GroupRoleMap.TryGetValue(c.Value, out var role) ? role : null)
            .OfType<string>()
            .ToHashSet();
        return Task.FromResult<IReadOnlySet<string>>(roles);
    }
}

/// <summary>Maps a login subject to an employee through the <c>security.user_accounts</c> table. Moves to ExternalIdMap (system SSO) when Integration provides it.</summary>
internal sealed class AccountEmployeeResolver(SecurityDbContext db) : IEmployeeResolver
{
    public async Task<Guid?> ResolveAsync(string subject, CancellationToken cancellationToken) =>
        await db.Accounts.AsNoTracking()
            .Where(a => a.Subject == subject)
            .Select(a => (Guid?)a.EmployeeId)
            .FirstOrDefaultAsync(cancellationToken);
}
