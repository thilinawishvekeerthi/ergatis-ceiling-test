using Talent.Shared.Events;

namespace Talent.Workflow.Contracts;

/// <summary>A workflow instance was started for a subject such as an IDP or a calibration session.</summary>
public sealed record WorkflowStarted(
    DateTimeOffset OccurredAt,
    Guid InstanceId,
    string DefinitionCode,
    string SubjectType,
    string SubjectId,
    string StartedBy) : DomainEvent(OccurredAt);

/// <summary>An instance moved from one state to another. Other modules (Notification, Idp) react to this.</summary>
public sealed record WorkflowTransitioned(
    DateTimeOffset OccurredAt,
    Guid InstanceId,
    string DefinitionCode,
    string SubjectType,
    string SubjectId,
    string From,
    string To,
    string Action,
    string Actor) : DomainEvent(OccurredAt);

/// <summary>An instance reached a final state.</summary>
public sealed record WorkflowCompleted(
    DateTimeOffset OccurredAt,
    Guid InstanceId,
    string DefinitionCode,
    string SubjectType,
    string SubjectId,
    string FinalState) : DomainEvent(OccurredAt);

/// <summary>An approval task passed its SLA and was escalated to more people.</summary>
public sealed record ApprovalTaskEscalated(
    DateTimeOffset OccurredAt,
    Guid InstanceId,
    Guid TaskId,
    string State,
    int Level,
    IReadOnlyList<string> NewAssignees) : DomainEvent(OccurredAt);

/// <summary>An approval task passed its SLA with no escalation left and no expiry action.</summary>
public sealed record ApprovalTaskSlaBreached(
    DateTimeOffset OccurredAt,
    Guid InstanceId,
    Guid TaskId,
    string State) : DomainEvent(OccurredAt);
