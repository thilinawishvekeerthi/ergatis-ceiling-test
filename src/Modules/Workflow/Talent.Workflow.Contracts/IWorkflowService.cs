namespace Talent.Workflow.Contracts;

public sealed record StartWorkflowRequest(
    string DefinitionCode,
    string SubjectType,
    string SubjectId,
    string? SubjectEmployeeId,
    IReadOnlyDictionary<string, string>? Context);

public sealed record WorkflowHistoryDto(
    int Sequence,
    string? FromState,
    string ToState,
    string Action,
    string Actor,
    string? Comment,
    DateTimeOffset OccurredAt);

public sealed record ApprovalTaskDto(
    Guid Id,
    Guid InstanceId,
    string State,
    ApprovalTaskStatus Status,
    DateTimeOffset? DueAt,
    int EscalationLevel,
    IReadOnlyList<string> Assignees);

public sealed record WorkflowInstanceDto(
    Guid Id,
    Guid DefinitionVersionId,
    string DefinitionCode,
    string SubjectType,
    string SubjectId,
    string CurrentState,
    WorkflowStatus Status,
    IReadOnlyList<string> AvailableActions,
    ApprovalTaskDto? OpenTask,
    IReadOnlyList<WorkflowHistoryDto> History);

/// <summary>What other modules use to run approvals. The caller is the current user.</summary>
public interface IWorkflowService
{
    /// <summary>Starts an instance on the newest published version of the definition and locks that version.</summary>
    Task<Guid> StartAsync(StartWorkflowRequest request, CancellationToken cancellationToken);

    /// <summary>Applies an action (Submit, Approve, Reject, ...) as the current user.</summary>
    Task TransitionAsync(Guid instanceId, string action, string? comment, CancellationToken cancellationToken);

    Task<WorkflowInstanceDto> GetAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<WorkflowInstanceDto?> FindForSubjectAsync(string subjectType, string subjectId, CancellationToken cancellationToken);

    /// <summary>Pending tasks the current user may act on, oldest due date first.</summary>
    Task<IReadOnlyList<ApprovalTaskDto>> GetInboxAsync(CancellationToken cancellationToken);

    /// <summary>Hands one task to another user. The current user stays an assignee.</summary>
    Task DelegateTaskAsync(Guid taskId, string toUserId, CancellationToken cancellationToken);

    /// <summary>Standing delegation (for example leave): new tasks for the current user also go to the delegate.</summary>
    Task<Guid> CreateDelegationAsync(string delegateUserId, DateTimeOffset validFrom, DateTimeOffset validTo, CancellationToken cancellationToken);

    Task RevokeDelegationAsync(Guid delegationId, CancellationToken cancellationToken);
}
