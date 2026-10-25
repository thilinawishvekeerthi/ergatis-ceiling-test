using System.Text.Json;
using Talent.Shared.Events;
using Talent.Workflow.Contracts;
using Talent.Workflow.Definitions;

namespace Talent.Workflow.Instances;

/// <summary>
/// One running workflow for one subject (an IDP, a calibration session). It pins the definition version it started on,
/// keeps the full history and raises an event for each transition (ADR-05, ADR-10).
/// </summary>
internal sealed class WorkflowInstance : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];
    private readonly List<HistoryEntry> _history = [];
    private readonly List<ApprovalTask> _tasks = [];
    private IReadOnlyDictionary<string, string>? _context;

    private WorkflowInstance()
    {
        DefinitionCode = "";
        SubjectType = "";
        SubjectId = "";
        CurrentState = "";
        StartedBy = "";
        ContextJson = "{}";
    }

    public Guid Id { get; private set; }
    public Guid DefinitionVersionId { get; private set; }
    public string DefinitionCode { get; private set; }
    public string SubjectType { get; private set; }
    public string SubjectId { get; private set; }
    public string? SubjectEmployeeId { get; private set; }
    public string CurrentState { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public string ContextJson { get; private set; }
    public string StartedBy { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Data the guards read, for example the estimated cost of an IDP. Fixed when the workflow starts.</summary>
    public IReadOnlyDictionary<string, string> Context =>
        _context ??= JsonSerializer.Deserialize<Dictionary<string, string>>(ContextJson) ?? [];

    /// <summary>Bumped on every change, so two people acting at once cannot both win (optimistic concurrency).</summary>
    public int Revision { get; private set; }

    public IReadOnlyCollection<HistoryEntry> History => _history;
    public IReadOnlyCollection<ApprovalTask> Tasks => _tasks;
    public ApprovalTask? OpenTask => _tasks.FirstOrDefault(t => t.Status == ApprovalTaskStatus.Pending);

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    public static WorkflowInstance Start(
        Guid definitionVersionId,
        string definitionCode,
        string subjectType,
        string subjectId,
        string? subjectEmployeeId,
        string startedBy,
        IReadOnlyDictionary<string, string> context,
        StateDefinition initial,
        TaskSpec? firstTask,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(startedBy);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(initial);

        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            DefinitionVersionId = definitionVersionId,
            DefinitionCode = definitionCode,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectEmployeeId = subjectEmployeeId,
            CurrentState = initial.Code,
            Status = WorkflowStatus.Active,
            ContextJson = JsonSerializer.Serialize(context),
            StartedBy = startedBy,
            StartedAt = now,
        };

        instance.AddHistory(null, initial.Code, "Start", startedBy, null, now);
        if (firstTask is not null)
        {
            instance._tasks.Add(new ApprovalTask(instance.Id, initial.Code, firstTask, now));
        }

        instance._events.Add(new WorkflowStarted(now, instance.Id, definitionCode, subjectType, subjectId, startedBy));
        return instance;
    }

    /// <summary>The caller may act if they are assigned to the open task, or, when no task is open, if they started the workflow.</summary>
    public void EnsureCanAct(string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (Status != WorkflowStatus.Active)
        {
            throw new WorkflowNotActiveException(Id);
        }

        var allowed = OpenTask is { } task ? task.Has(actor) : actor == StartedBy;
        if (!allowed)
        {
            throw new ActorNotAllowedException(CurrentState);
        }
    }

    /// <summary>Moves to <paramref name="target"/>. The caller has already checked the transition against the definition.</summary>
    public void Apply(
        TransitionDefinition transition,
        StateDefinition target,
        string actor,
        string? comment,
        DateTimeOffset now,
        TaskSpec? nextTask)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        if (Status != WorkflowStatus.Active)
        {
            throw new WorkflowNotActiveException(Id);
        }

        if (transition.From != CurrentState)
        {
            throw new ActionNotAllowedException(CurrentState, transition.Action);
        }

        if (transition.RequiresComment && string.IsNullOrWhiteSpace(comment))
        {
            throw new CommentRequiredException(transition.Action);
        }

        OpenTask?.Complete(actor, now);

        var from = CurrentState;
        CurrentState = target.Code;
        AddHistory(from, target.Code, transition.Action, actor, comment?.Trim(), now);
        _events.Add(new WorkflowTransitioned(now, Id, DefinitionCode, SubjectType, SubjectId, from, target.Code, transition.Action, actor));

        if (target.IsFinal)
        {
            Status = WorkflowStatus.Completed;
            CompletedAt = now;
            _events.Add(new WorkflowCompleted(now, Id, DefinitionCode, SubjectType, SubjectId, target.Code));
        }
        else if (nextTask is not null)
        {
            _tasks.Add(new ApprovalTask(Id, target.Code, nextTask, now));
        }

        Revision++;
    }

    public void DelegateTask(string by, string to, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        var task = OpenTask ?? throw new ActorNotAllowedException(CurrentState);
        if (!task.Has(by))
        {
            throw new ActorNotAllowedException(CurrentState);
        }

        if (task.AddAssignee(new Assignment(to, by)))
        {
            AddHistory(CurrentState, CurrentState, "Delegate", by, null, now);
            Revision++;
        }
    }

    public void Escalate(IReadOnlyList<Assignment> additional, DateTimeOffset? nextDue, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(additional);
        var task = OpenTask ?? throw new WorkflowNotActiveException(Id);
        var added = additional.Where(task.AddAssignee).Select(a => a.UserId).ToList();
        task.RegisterEscalation(nextDue);
        AddHistory(CurrentState, CurrentState, "Escalate", SystemActor, null, now);
        _events.Add(new ApprovalTaskEscalated(now, Id, task.Id, task.State, task.EscalationLevel, added));
        Revision++;
    }

    public void MarkSlaBreached(DateTimeOffset now)
    {
        var task = OpenTask ?? throw new WorkflowNotActiveException(Id);
        task.MarkBreached(now);
        _events.Add(new ApprovalTaskSlaBreached(now, Id, task.Id, task.State));
        Revision++;
    }

    /// <summary>The actor recorded for automatic steps such as SLA escalation.</summary>
    public const string SystemActor = "system";

    private void AddHistory(string? from, string to, string action, string actor, string? comment, DateTimeOffset at) =>
        _history.Add(new HistoryEntry(Id, _history.Count + 1, from, to, action, actor, comment, at));
}
