using Talent.Shared.Events;

namespace Talent.Assessment.Contracts;

/// <summary>A review cycle opened. Notification tells the reviewers.</summary>
public sealed record ReviewCycleOpened(
    DateTimeOffset OccurredAt,
    Guid CycleId,
    string Name) : DomainEvent(OccurredAt);

/// <summary>A manager rated an employee.</summary>
public sealed record ReviewRated(
    DateTimeOffset OccurredAt,
    Guid ReviewId,
    Guid CycleId,
    Guid EmployeeId) : DomainEvent(OccurredAt);

/// <summary>Calibration moved an employee to another grid cell.</summary>
public sealed record PlacementCalibrated(
    DateTimeOffset OccurredAt,
    Guid ReviewId,
    Guid CycleId,
    Guid EmployeeId,
    int FromColumn,
    int FromRow,
    int ToColumn,
    int ToRow) : DomainEvent(OccurredAt);
