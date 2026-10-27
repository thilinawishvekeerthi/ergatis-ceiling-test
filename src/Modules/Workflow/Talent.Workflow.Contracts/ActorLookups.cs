namespace Talent.Workflow.Contracts;

/// <summary>
/// Plug point: who is the manager of an employee. Integration or Security supplies the real one from synced data.
/// Never call a source system from a request (principle 6).
/// </summary>
public interface IManagerLookup
{
    Task<string?> GetManagerUserIdAsync(string employeeId, CancellationToken cancellationToken);
}

/// <summary>Plug point: which users hold a role. The role source belongs to Security (ADR-07).</summary>
public interface IRoleMemberLookup
{
    Task<IReadOnlyList<string>> GetUserIdsAsync(string roleCode, CancellationToken cancellationToken);
}
