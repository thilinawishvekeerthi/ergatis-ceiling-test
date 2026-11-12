using Talent.Shared.Events;

namespace Talent.Assessment.Contracts;

/// <summary>A reviewer placed an employee on the nine-box. Carries ids and numbers only, no names.</summary>
public sealed record NineBoxEvaluationRecorded(
    DateTimeOffset OccurredAt,
    Guid EvaluationId,
    Guid EmployeeId,
    Guid GridVersionId,
    int BoxNumber,
    int TalentScore) : DomainEvent(OccurredAt);
