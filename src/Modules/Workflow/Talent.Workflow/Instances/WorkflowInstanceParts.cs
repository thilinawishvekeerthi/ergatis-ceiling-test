using Talent.Workflow.Contracts;

namespace Talent.Workflow.Instances;

/// <summary>One person an approval task is assigned to. <see cref="DelegatedFrom"/> is set when they act for someone else.</summary>
internal sealed record Assignment(string UserId, string? DelegatedFrom);

/// <summary>What the next approval task looks like. Null when the target state needs no approval.</summary>
internal sealed record TaskSpec(IReadOnlyList<Assignment> Assignees, DateTimeOffset? DueAt);

/// <summary>One row of the instance history. Insert-only.</summary>
internal sealed class HistoryEntry
{
    private HistoryEntry()
    {
        ToState = "";
        Action = "";
        Actor = "";
    }

    internal HistoryEntry(Guid instanceId, int sequence, string? fromState, string toState, string action, string actor, string? comment, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        InstanceId = instanceId;
        Sequence = sequence;
        FromState = fromState;
        ToState = toState;
        Action = action;
        Actor = actor;
        Comment = comment;
        OccurredAt = at;
    }

    public Guid Id { get; private set; }
    public Guid InstanceId { get; private set; }
    public int Sequence { get; private set; }
    public string? FromState { get; private set; }
    public string ToState { get; private set; }
    public string Action { get; private set; }
    public string Actor { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}

internal sealed class TaskAssignee
{
    private TaskAssignee() => UserId = "";

    internal TaskAssignee(Guid taskId, string userId, string? delegatedFrom)
    {
        TaskId = taskId;
        UserId = userId;
        DelegatedFrom = delegatedFrom;
    }

    public Guid TaskId { get; private set; }
    public string UserId { get; private set; }
    public string? DelegatedFrom { get; private set; }
}

/// <summary>The approval a state is waiting for. Any assignee may complete it.</summary>
internal sealed class ApprovalTask
{
    private readonly List<TaskAssignee> _assignees = [];

    private ApprovalTask() => State = "";

    internal ApprovalTask(Guid instanceId, string state, TaskSpec spec, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        InstanceId = instanceId;
        State = state;
        Status = ApprovalTaskStatus.Pending;
        DueAt = spec.DueAt;
        CreatedAt = now;
        foreach (var a in spec.Assignees)
        {
            AddAssignee(a);
        }
    }

    public Guid Id { get; private set; }
    public Guid InstanceId { get; private set; }
    public string State { get; private set; }
    public ApprovalTaskStatus Status { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public int EscalationLevel { get; private set; }
    public DateTimeOffset? SlaBreachedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }

    public IReadOnlyCollection<TaskAssignee> Assignees => _assignees;

    internal bool Has(string userId) => _assignees.Any(a => a.UserId == userId);

    internal bool AddAssignee(Assignment assignment)
    {
        if (Has(assignment.UserId))
        {
            return false;
        }

        _assignees.Add(new TaskAssignee(Id, assignment.UserId, assignment.DelegatedFrom));
        return true;
    }

    internal void Complete(string actor, DateTimeOffset now)
    {
        Status = ApprovalTaskStatus.Completed;
        CompletedBy = actor;
        CompletedAt = now;
    }

    internal void RegisterEscalation(DateTimeOffset? nextDue)
    {
        EscalationLevel++;
        DueAt = nextDue;
    }

    internal void MarkBreached(DateTimeOffset now) => SlaBreachedAt = now;
}

/// <summary>A standing hand-over of approvals, for example while on leave.</summary>
internal sealed class Delegation
{
    private Delegation()
    {
        DelegatorId = "";
        DelegateId = "";
    }

    public Guid Id { get; private set; }
    public string DelegatorId { get; private set; }
    public string DelegateId { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidTo { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static Delegation Create(string delegatorId, string delegateId, DateTimeOffset validFrom, DateTimeOffset validTo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(delegatorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(delegateId);
        if (delegatorId == delegateId)
        {
            throw new WorkflowConflictException("You cannot delegate to yourself.");
        }

        if (validTo <= validFrom)
        {
            throw new WorkflowConflictException("A delegation must end after it starts.");
        }

        return new Delegation { Id = Guid.NewGuid(), DelegatorId = delegatorId, DelegateId = delegateId, ValidFrom = validFrom, ValidTo = validTo };
    }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ValidFrom <= now && now < ValidTo;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
