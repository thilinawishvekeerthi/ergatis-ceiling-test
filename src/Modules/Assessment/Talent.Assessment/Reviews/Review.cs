using Talent.Assessment.Contracts;
using Talent.Assessment.Cycles;
using Talent.Config.Contracts;
using Talent.Shared.Events;

namespace Talent.Assessment.Reviews;

/// <summary>
/// One employee's review in one cycle: the manager's performance and potential ratings and where that lands on the grid.
/// The proposed cell stays next to the final cell, so a calibration change is always visible with its author and reason (ADR-03).
/// </summary>
internal sealed class Review : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    // For EF Core.
    private Review()
    {
    }

    private Review(Guid cycleId, Guid employeeId, Guid reviewerId)
    {
        Id = Guid.NewGuid();
        CycleId = cycleId;
        EmployeeId = employeeId;
        ReviewerId = reviewerId;
        Status = ReviewStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ReviewerId { get; private set; }
    public ReviewStatus Status { get; private set; }
    public int? PerformanceLevel { get; private set; }
    public int? PotentialLevel { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset? RatedAt { get; private set; }
    public int? ProposedColumn { get; private set; }
    public int? ProposedRow { get; private set; }
    public int? FinalColumn { get; private set; }
    public int? FinalRow { get; private set; }
    public string? CalibratedBy { get; private set; }
    public string? CalibrationReason { get; private set; }
    public DateTimeOffset? CalibratedAt { get; private set; }

    public bool IsCalibrated => CalibratedBy is not null;

    public GridCell? FinalCell => FinalColumn is { } c && FinalRow is { } r ? new GridCell(c, r) : null;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    public static Review Create(Guid cycleId, Guid employeeId, Guid reviewerId)
    {
        if (employeeId == Guid.Empty || reviewerId == Guid.Empty)
        {
            throw new InvalidReviewException("A review needs an employee and a reviewer.");
        }

        if (employeeId == reviewerId)
        {
            throw new InvalidReviewException("A reviewer cannot rate their own review.");
        }

        return new Review(cycleId, employeeId, reviewerId);
    }

    /// <summary>
    /// Records the ratings and the cell they land on. Rating again replaces the earlier rating and clears any calibration,
    /// because calibration happens after the cycle is no longer open.
    /// </summary>
    public void Rate(int performanceLevel, int potentialLevel, string? comment, GridCell cell, DateTimeOffset now)
    {
        if (performanceLevel < 1 || potentialLevel < 1)
        {
            throw new InvalidReviewException("Ratings must be 1 or higher.");
        }

        if (comment is { Length: > 2000 })
        {
            throw new InvalidReviewException("The comment can be at most 2000 characters.");
        }

        PerformanceLevel = performanceLevel;
        PotentialLevel = potentialLevel;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        ProposedColumn = FinalColumn = cell.Column;
        ProposedRow = FinalRow = cell.Row;
        CalibratedBy = null;
        CalibrationReason = null;
        CalibratedAt = null;
        RatedAt = now;
        Status = ReviewStatus.Rated;
        _events.Add(new ReviewRated(now, Id, CycleId, EmployeeId));
    }

    /// <summary>Moves the final cell. Only for grids in CalculatedWithOverride mode, with an author and a reason.</summary>
    public void Calibrate(GridCell cell, PlacementMode mode, string calibratedBy, string reason, DateTimeOffset now)
    {
        if (Status != ReviewStatus.Rated || FinalCell is not { } from)
        {
            throw new AssessmentConflictException("Only a rated review can be calibrated.");
        }

        if (mode != PlacementMode.CalculatedWithOverride)
        {
            throw new AssessmentConflictException($"This grid places people in {mode} mode, so a placement cannot be overridden.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidReviewException("A calibration change needs a reason.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(calibratedBy);
        if (from == cell)
        {
            throw new InvalidReviewException("The employee is already in that cell.");
        }

        FinalColumn = cell.Column;
        FinalRow = cell.Row;
        CalibratedBy = calibratedBy;
        CalibrationReason = reason.Trim();
        CalibratedAt = now;
        _events.Add(new PlacementCalibrated(now, Id, CycleId, EmployeeId, from.Column, from.Row, cell.Column, cell.Row));
    }
}
