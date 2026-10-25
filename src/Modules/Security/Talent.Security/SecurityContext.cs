using Talent.Security.Contracts;

namespace Talent.Security;

/// <summary>The signed-in caller for one request. The middleware fills it once; before that it is anonymous.</summary>
internal sealed class SecurityContext : ISecurityUser
{
    private static readonly IReadOnlySet<string> NoRoles = new HashSet<string>();

    public bool IsAuthenticated { get; private set; }

    public string UserId { get; private set; } = "anonymous";

    public Guid? EmployeeId { get; private set; }

    public IReadOnlySet<string> Roles { get; private set; } = NoRoles;

    public void SignIn(string userId, Guid? employeeId, IReadOnlySet<string> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(roles);
        IsAuthenticated = true;
        UserId = userId;
        EmployeeId = employeeId;
        Roles = roles;
    }

    public static SecurityContext ForTest(string userId, Guid? employeeId, IEnumerable<string> roles)
    {
        var context = new SecurityContext();
        context.SignIn(userId, employeeId, roles.ToHashSet());
        return context;
    }
}
