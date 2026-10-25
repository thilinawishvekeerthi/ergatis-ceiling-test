using Microsoft.EntityFrameworkCore;
using Talent.Workflow.Contracts;
using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;
using Talent.Workflow.Persistence;

namespace Talent.Workflow;

/// <summary>Plug point (ADR-05): turns an actor reference from a definition into user ids.</summary>
internal interface IActorResolver
{
    ActorKind Kind { get; }

    Task<IReadOnlyList<string>> ResolveAsync(ActorRef actor, string? subjectEmployeeId, CancellationToken cancellationToken);
}

internal sealed class ManagerActorResolver(IManagerLookup managers) : IActorResolver
{
    public ActorKind Kind => ActorKind.Manager;

    public async Task<IReadOnlyList<string>> ResolveAsync(ActorRef actor, string? subjectEmployeeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subjectEmployeeId))
        {
            throw new ActorResolutionException("The manager cannot be found because the workflow has no employee.");
        }

        var manager = await managers.GetManagerUserIdAsync(subjectEmployeeId, cancellationToken);
        return string.IsNullOrWhiteSpace(manager) ? [] : [manager];
    }
}

internal sealed class RoleActorResolver(IRoleMemberLookup roles) : IActorResolver
{
    public ActorKind Kind => ActorKind.Role;

    public async Task<IReadOnlyList<string>> ResolveAsync(ActorRef actor, string? subjectEmployeeId, CancellationToken cancellationToken) =>
        await roles.GetUserIdsAsync(actor.Value!, cancellationToken);
}

internal sealed class UserActorResolver : IActorResolver
{
    public ActorKind Kind => ActorKind.User;

    public Task<IReadOnlyList<string>> ResolveAsync(ActorRef actor, string? subjectEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([actor.Value!]);
}

/// <summary>Used until Integration and Security supply the real lookups. Nobody is found, so a Manager step fails loudly.</summary>
internal sealed class EmptyManagerLookup : IManagerLookup
{
    public Task<string?> GetManagerUserIdAsync(string employeeId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}

internal sealed class EmptyRoleMemberLookup : IRoleMemberLookup
{
    public Task<IReadOnlyList<string>> GetUserIdsAsync(string roleCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

/// <summary>Resolves an approver to people, adds standing delegates, and plans the approval task of a state.</summary>
internal sealed class TaskPlanner(IEnumerable<IActorResolver> resolvers, WorkflowDbContext db, TimeProvider clock)
{
    public async Task<TaskSpec?> PlanAsync(StateDefinition state, string? subjectEmployeeId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Approval is not { } approval)
        {
            return null;
        }

        var assignees = await ResolveAsync(approval.Approver, subjectEmployeeId, state.Code, cancellationToken);
        var due = approval.Sla is { } sla ? clock.GetUtcNow().AddHours(sla.Hours) : (DateTimeOffset?)null;
        return new TaskSpec(assignees, due);
    }

    /// <summary>People for an actor reference, plus the active standing delegates of each. Throws when nobody is found.</summary>
    public async Task<IReadOnlyList<Assignment>> ResolveAsync(
        ActorRef actor, string? subjectEmployeeId, string stateCode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var resolver = resolvers.SingleOrDefault(r => r.Kind == actor.Kind)
                       ?? throw new ActorResolutionException($"No resolver for actor kind {actor.Kind}.");
        var users = (await resolver.ResolveAsync(actor, subjectEmployeeId, cancellationToken)).Distinct().ToList();
        if (users.Count == 0)
        {
            throw new ActorResolutionException($"Nobody could be found for the {actor.Kind} approval of state {stateCode}.");
        }

        var now = clock.GetUtcNow();
        var delegations = await db.Delegations.AsNoTracking()
            .Where(d => users.Contains(d.DelegatorId) && d.RevokedAt == null && d.ValidFrom <= now && now < d.ValidTo)
            .OrderBy(d => d.ValidFrom).ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);

        var result = users.Select(u => new Assignment(u, null)).ToList();
        foreach (var d in delegations.Where(d => result.All(r => r.UserId != d.DelegateId)))
        {
            result.Add(new Assignment(d.DelegateId, d.DelegatorId));
        }

        return result;
    }
}
