using Talent.Shared.Events;

namespace Talent.Career.Contracts;

/// <summary>An employee's competency levels or current job changed. Other modules (Notification, IDP) react to this.</summary>
public sealed record EmployeeProfileUpdated(
    DateTimeOffset OccurredAt,
    Guid EmployeeId,
    Guid? CurrentJobId,
    int CompetencyCount) : DomainEvent(OccurredAt);
