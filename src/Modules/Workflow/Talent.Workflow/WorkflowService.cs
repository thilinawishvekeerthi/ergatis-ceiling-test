using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talent.Config.Contracts;
using Talent.Shared.Auditing;
using Talent.Shared.Security;
using Talent.Workflow.Contracts;
using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;
using Talent.Workflow.Persistence;
using Talent.Workflow.Versions;

namespace Talent.Workflow;

/// <summary>The workflow engine other modules call. The caller is the current user. Every change is audited.</summary>
internal sealed class WorkflowService(
    WorkflowDbContext db,
    ICurrentUser user,
    TimeProvider clock,
    TaskPlanner planner,
    TransitionExecutor executor) : IWorkflowService
{
    private const int InboxLimit = 200;
    private const int StartedLimit = 50;

    public async Task<Guid> StartAsync(StartWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var latest = await (
            from v in db.Versions
            join d in db.Definitions on v.DefinitionId equals d.Id
            where d.Code == request.DefinitionCode
                  && (v.Status == ConfigVersionStatus.Published || v.Status == ConfigVersionStatus.Locked)
            orderby v.VersionNo descending
            select v).FirstOrDefaultAsync(cancellationToken)
            ?? throw new WorkflowNotFoundException("Published workflow", request.DefinitionCode);

        var model = WorkflowDefinitionModel.Parse(latest.PayloadJson);
        var initial = model.GetState(model.InitialState);
        var firstTask = await planner.PlanAsync(initial, request.SubjectEmployeeId, cancellationToken);

        latest.Lock();
        var instance = WorkflowInstance.Start(
            latest.Id, request.DefinitionCode, request.SubjectType, request.SubjectId, request.SubjectEmployeeId,
            user.UserId, request.Context ?? new Dictionary<string, string>(), initial, firstTask, clock.GetUtcNow());

        db.Instances.Add(instance);
        db.AddAudit(user, clock, "Start", nameof(WorkflowInstance), instance.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { instance.DefinitionCode, instance.SubjectType, instance.SubjectId, version = latest.VersionNo }));
        await SaveAsync(cancellationToken);
        return instance.Id;
    }

    public async Task TransitionAsync(Guid instanceId, string action, string? comment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var instance = await LoadAsync(instanceId, cancellationToken);
        var model = await LoadModelAsync(instance, cancellationToken);
        var before = instance.CurrentState;

        await executor.ExecuteAsync(instance, model, action, user.UserId, comment, checkActor: true, cancellationToken);

        db.AddAudit(user, clock, action, nameof(WorkflowInstance), instance.Id.ToString(),
            beforeJson: JsonSerializer.Serialize(new { state = before }),
            afterJson: JsonSerializer.Serialize(new { state = instance.CurrentState, status = instance.Status.ToString() }));
        await SaveAsync(cancellationToken);
    }

    public async Task<WorkflowInstanceDto> GetAsync(Guid instanceId, CancellationToken cancellationToken) =>
        await ToDtoAsync(await LoadAsync(instanceId, cancellationToken, track: false), cancellationToken);

    public async Task<WorkflowInstanceDto?> FindForSubjectAsync(string subjectType, string subjectId, CancellationToken cancellationToken)
    {
        var instance = await db.Instances.AsNoTracking()
            .Include(i => i.History)
            .Include(i => i.Tasks).ThenInclude(t => t.Assignees)
            .Where(i => i.SubjectType == subjectType && i.SubjectId == subjectId)
            .OrderByDescending(i => i.StartedAt)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
        return instance is null ? null : await ToDtoAsync(instance, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowInstanceDto>> ListStartedByMeAsync(CancellationToken cancellationToken)
    {
        var instances = await db.Instances.AsNoTracking()
            .Include(i => i.History)
            .Include(i => i.Tasks).ThenInclude(t => t.Assignees)
            .Where(i => i.StartedBy == user.UserId)
            .OrderByDescending(i => i.StartedAt).ThenBy(i => i.Id)
            .Take(StartedLimit)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var result = new List<WorkflowInstanceDto>(instances.Count);
        foreach (var instance in instances)
        {
            result.Add(await ToDtoAsync(instance, cancellationToken));
        }

        return result;
    }

    public async Task<IReadOnlyList<ApprovalTaskDto>> GetInboxAsync(CancellationToken cancellationToken)
    {
        var me = user.UserId;
        var tasks = await (
            from t in db.Tasks.AsNoTracking()
            join a in db.Assignees on t.Id equals a.TaskId
            where a.UserId == me && t.Status == ApprovalTaskStatus.Pending
            orderby t.DueAt == null, t.DueAt, t.CreatedAt, t.Id
            select t).Take(InboxLimit).ToListAsync(cancellationToken);

        var ids = tasks.Select(t => t.Id).ToList();
        var assignees = await db.Assignees.AsNoTracking().Where(a => ids.Contains(a.TaskId)).ToListAsync(cancellationToken);
        return [.. tasks.Select(t => ToDto(t, assignees.Where(a => a.TaskId == t.Id).Select(a => a.UserId)))];
    }

    public async Task DelegateTaskAsync(Guid taskId, string toUserId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toUserId);
        var instanceId = await db.Tasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.InstanceId).FirstOrDefaultAsync(cancellationToken)
                         ?? throw new WorkflowNotFoundException("Approval task", taskId.ToString());
        var instance = await LoadAsync(instanceId, cancellationToken);
        if (instance.OpenTask?.Id != taskId)
        {
            throw new WorkflowConflictException("That approval task is already closed.");
        }

        instance.DelegateTask(user.UserId, toUserId, clock.GetUtcNow());
        db.AddAudit(user, clock, "DelegateTask", nameof(ApprovalTask), taskId.ToString(),
            afterJson: JsonSerializer.Serialize(new { to = toUserId }));
        await SaveAsync(cancellationToken);
    }

    public async Task<Guid> CreateDelegationAsync(
        string delegateUserId, DateTimeOffset validFrom, DateTimeOffset validTo, CancellationToken cancellationToken)
    {
        var delegation = Delegation.Create(user.UserId, delegateUserId, validFrom, validTo);
        db.Delegations.Add(delegation);
        db.AddAudit(user, clock, "CreateDelegation", nameof(Delegation), delegation.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { to = delegateUserId, validFrom, validTo }));
        await SaveAsync(cancellationToken);
        return delegation.Id;
    }

    public async Task RevokeDelegationAsync(Guid delegationId, CancellationToken cancellationToken)
    {
        var delegation = await db.Delegations.SingleOrDefaultAsync(d => d.Id == delegationId, cancellationToken)
                         ?? throw new WorkflowNotFoundException("Delegation", delegationId.ToString());
        if (delegation.DelegatorId != user.UserId)
        {
            throw new ActorNotAllowedException("delegation");
        }

        delegation.Revoke(clock.GetUtcNow());
        db.AddAudit(user, clock, "RevokeDelegation", nameof(Delegation), delegationId.ToString());
        await SaveAsync(cancellationToken);
    }

    private async Task<WorkflowInstance> LoadAsync(Guid instanceId, CancellationToken cancellationToken, bool track = true)
    {
        var query = db.Instances
            .Include(i => i.History)
            .Include(i => i.Tasks).ThenInclude(t => t.Assignees)
            .AsSplitQuery();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(i => i.Id == instanceId, cancellationToken)
               ?? throw new WorkflowNotFoundException("Workflow", instanceId.ToString());
    }

    private async Task<WorkflowDefinitionModel> LoadModelAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        var payload = await db.Versions.AsNoTracking()
            .Where(v => v.Id == instance.DefinitionVersionId).Select(v => v.PayloadJson)
            .SingleAsync(cancellationToken);
        return WorkflowDefinitionModel.Parse(payload);
    }

    private async Task<WorkflowInstanceDto> ToDtoAsync(WorkflowInstance instance, CancellationToken cancellationToken)
    {
        var model = await LoadModelAsync(instance, cancellationToken);
        var actions = instance.Status == WorkflowStatus.Active
            ? model.AvailableActions(instance.CurrentState, instance.Context)
            : [];
        var open = instance.OpenTask;
        return new WorkflowInstanceDto(
            instance.Id, instance.DefinitionVersionId, instance.DefinitionCode, instance.SubjectType, instance.SubjectId,
            instance.CurrentState, instance.Status, actions,
            open is null ? null : ToDto(open, open.Assignees.Select(a => a.UserId)),
            [.. instance.History.OrderBy(h => h.Sequence).Select(h =>
                new WorkflowHistoryDto(h.Sequence, h.FromState, h.ToState, h.Action, h.Actor, h.Comment, h.OccurredAt))],
            instance.StartedBy, instance.StartedAt, instance.Context);
    }

    private static ApprovalTaskDto ToDto(ApprovalTask task, IEnumerable<string> assignees) =>
        new(task.Id, task.InstanceId, task.State, task.Status, task.DueAt, task.EscalationLevel, [.. assignees.Order(StringComparer.Ordinal)]);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WorkflowConflictException("Someone else changed this workflow. Reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new WorkflowConflictException("A workflow is already running for this subject.");
        }
    }
}
