using Talent.Assessment.Contracts;
using Talent.Shared.Events;

namespace Talent.Assessment.NineBox;

/// <summary>
/// One reviewer's nine-box evaluation of one employee. Insert-only: a re-evaluation is a new row, so history is kept.
/// The derived box, label, score and band are stored as a snapshot of the pinned grid version, so history
/// still shows what the reviewer saw after the client publishes new labels.
/// </summary>
internal sealed class NineBoxEvaluation : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    private NineBoxEvaluation()
    {
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid GridVersionId { get; private set; }
    public int PerformanceLevel { get; private set; }
    public int PotentialLevel { get; private set; }
    public int BoxNumber { get; private set; }
    public string BoxLabel { get; private set; } = "";
    public int TalentScore { get; private set; }
    public string Band { get; private set; } = "";
    public string ReviewerId { get; private set; } = "";
    public DateTimeOffset EvaluatedAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    public static NineBoxEvaluation Record(
        Guid employeeId, Guid gridVersionId, NineBoxPlacement placement, string reviewerId, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerId);

        // PostgreSQL keeps microseconds. Trim here so the time returned to the caller is the time stored.
        evaluatedAt = new DateTimeOffset(evaluatedAt.UtcTicks - (evaluatedAt.UtcTicks % 10), TimeSpan.Zero);

        var evaluation = new NineBoxEvaluation
        {
            // Version 7 ids sort by time, which keeps the history index append-friendly.
            Id = Guid.CreateVersion7(evaluatedAt),
            EmployeeId = employeeId,
            GridVersionId = gridVersionId,
            PerformanceLevel = placement.Performance,
            PotentialLevel = placement.Potential,
            BoxNumber = placement.BoxNumber,
            BoxLabel = placement.Label,
            TalentScore = placement.TalentScore,
            Band = placement.Band,
            ReviewerId = reviewerId,
            EvaluatedAt = evaluatedAt,
        };
        evaluation._events.Add(new NineBoxEvaluationRecorded(
            evaluatedAt, evaluation.Id, employeeId, gridVersionId, placement.BoxNumber, placement.TalentScore));
        return evaluation;
    }

    public void ClearDomainEvents() => _events.Clear();
}

/// <summary>Points at an employee's newest evaluation, so "latest placement" is one primary-key read however long the history grows.</summary>
internal sealed class LatestNineBoxPlacement
{
    public Guid EmployeeId { get; set; }
    public Guid EvaluationId { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}
