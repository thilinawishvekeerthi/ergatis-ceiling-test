using Talent.Assessment.Contracts;
using Talent.Shared.Events;

namespace Talent.Assessment.Cycles;

/// <summary>
/// One review round. It pins the two scales and the grid by version id when it opens (ADR-02),
/// so later config changes never change a running cycle. Draft → Open → Calibration → Closed.
/// </summary>
internal sealed class ReviewCycle : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    // For EF Core.
    private ReviewCycle() => Name = "";

    private ReviewCycle(string name, DateOnly start, DateOnly end, Guid performanceScale, Guid potentialScale, Guid grid)
    {
        Id = Guid.NewGuid();
        Name = name;
        PeriodStart = start;
        PeriodEnd = end;
        PerformanceScaleVersionId = performanceScale;
        PotentialScaleVersionId = potentialScale;
        GridVersionId = grid;
        Status = CycleStatus.Draft;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public CycleStatus Status { get; private set; }
    public Guid PerformanceScaleVersionId { get; private set; }
    public Guid PotentialScaleVersionId { get; private set; }
    public Guid GridVersionId { get; private set; }
    public DateTimeOffset? OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    public static ReviewCycle Create(string name, DateOnly start, DateOnly end, Guid performanceScale, Guid potentialScale, Guid grid)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
        {
            throw new InvalidReviewException("The cycle name must be 1 to 200 characters.");
        }

        if (end < start)
        {
            throw new InvalidReviewException("The cycle cannot end before it starts.");
        }

        return new ReviewCycle(name.Trim(), start, end, performanceScale, potentialScale, grid);
    }

    /// <param name="reviewerIds">Everyone assigned to rate in this cycle. They go into the event so Notification can tell them.</param>
    public void Open(DateTimeOffset now, IEnumerable<Guid>? reviewerIds = null)
    {
        Require("open", CycleStatus.Draft);
        Status = CycleStatus.Open;
        OpenedAt = now;
        _events.Add(new ReviewCycleOpened(now, Id, Name, [.. (reviewerIds ?? []).Distinct()]));
    }

    public void StartCalibration()
    {
        Require("start calibration of", CycleStatus.Open);
        Status = CycleStatus.Calibration;
    }

    public void Close(DateTimeOffset now)
    {
        Require("close", CycleStatus.Calibration);
        Status = CycleStatus.Closed;
        ClosedAt = now;
    }

    /// <summary>Ratings can be given or changed only while the cycle is open.</summary>
    public void RequireOpenForRating() => Require("rate in", CycleStatus.Open);

    /// <summary>Reviews are assigned while the cycle is being set up or open.</summary>
    public void RequireOpenForAssignment() => Require("assign reviews in", CycleStatus.Draft, CycleStatus.Open);

    /// <summary>Placements move only while the committee calibrates.</summary>
    public void RequireCalibration() => Require("calibrate", CycleStatus.Calibration);

    private void Require(string action, params CycleStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidCycleTransitionException(action, Status.ToString());
        }
    }
}
