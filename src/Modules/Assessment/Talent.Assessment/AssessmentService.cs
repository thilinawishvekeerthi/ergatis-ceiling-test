using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talent.Assessment.Cycles;
using Talent.Assessment.Persistence;
using Talent.Assessment.Reviews;
using Talent.Config.Contracts;
using Talent.Shared.Auditing;
using Talent.Shared.Security;

namespace Talent.Assessment;

/// <summary>
/// Assessment operations: set up and run a cycle, rate, calibrate. No permission checks here (the API service does those),
/// so seeding and background jobs can use it. Every change is audited. Rating and calibration audits are confidential.
/// </summary>
internal sealed class AssessmentService(
    AssessmentDbContext db,
    IConfigVersionReader configs,
    IConfigVersionLocker locker,
    IGridService grids,
    ICurrentUser user,
    TimeProvider clock)
{
    public const string PerformanceAxis = "Performance";
    public const string PotentialAxis = "Potential";

    public async Task<ReviewCycle> CreateCycleAsync(
        string name, DateOnly start, DateOnly end, string performanceScaleCode, string potentialScaleCode, string gridCode,
        CancellationToken cancellationToken)
    {
        var performance = await LatestAsync(ConfigType.Scale, performanceScaleCode, cancellationToken);
        var potential = await LatestAsync(ConfigType.Scale, potentialScaleCode, cancellationToken);
        var grid = await LatestAsync(ConfigType.Grid, gridCode, cancellationToken);

        var cycle = ReviewCycle.Create(name, start, end, performance.Id, potential.Id, grid.Id);
        if (await db.Cycles.AnyAsync(c => c.Name == cycle.Name, cancellationToken))
        {
            throw new AssessmentConflictException($"A cycle named '{cycle.Name}' already exists.");
        }

        db.Cycles.Add(cycle);
        db.AddAudit(user, clock, "CreateCycle", nameof(ReviewCycle), cycle.Id.ToString(),
            afterJson: Json.Write(new { cycle.Name, performance = performance.Code, potential = potential.Code, grid = grid.Code }));
        await SaveAsync(cancellationToken);
        return cycle;
    }

    /// <summary>Opening pins the config: the two scales and the grid become locked, so they can no longer change under the cycle.</summary>
    public async Task<ReviewCycle> OpenCycleAsync(Guid cycleId, CancellationToken cancellationToken)
    {
        var cycle = await GetCycleAsync(cycleId, cancellationToken);
        var reviewers = await db.Reviews.Where(r => r.CycleId == cycleId).Select(r => r.ReviewerId).Distinct().ToListAsync(cancellationToken);
        cycle.Open(clock.GetUtcNow(), reviewers);
        foreach (var versionId in new[] { cycle.PerformanceScaleVersionId, cycle.PotentialScaleVersionId, cycle.GridVersionId })
        {
            await locker.LockAsync(versionId, cancellationToken);
        }

        db.AddAudit(user, clock, "OpenCycle", nameof(ReviewCycle), cycle.Id.ToString());
        await SaveAsync(cancellationToken);
        return cycle;
    }

    public Task<ReviewCycle> StartCalibrationAsync(Guid cycleId, CancellationToken cancellationToken) =>
        ChangeAsync(cycleId, "StartCalibration", c => c.StartCalibration(), cancellationToken);

    public Task<ReviewCycle> CloseCycleAsync(Guid cycleId, CancellationToken cancellationToken) =>
        ChangeAsync(cycleId, "CloseCycle", c => c.Close(clock.GetUtcNow()), cancellationToken);

    public async Task<Review> AssignAsync(Guid cycleId, Guid employeeId, Guid reviewerId, CancellationToken cancellationToken)
    {
        var cycle = await GetCycleAsync(cycleId, cancellationToken);
        cycle.RequireOpenForAssignment();

        var review = Review.Create(cycleId, employeeId, reviewerId);
        db.Reviews.Add(review);
        db.AddAudit(user, clock, "AssignReview", nameof(Review), review.Id.ToString(),
            afterJson: Json.Write(new { cycleId, employeeId, reviewerId }));
        await SaveAsync(cancellationToken, $"{employeeId} already has a review in this cycle.");
        return review;
    }

    /// <summary>
    /// Rates an employee. The cell comes from the grid's rules: calculated from the two scores, or chosen by the manager
    /// when the grid is in Manual mode (<paramref name="column"/> and <paramref name="row"/> are required then).
    /// </summary>
    public async Task<Review> RateAsync(
        Guid reviewId, int performanceLevel, int potentialLevel, string? comment, int? column, int? row, CancellationToken cancellationToken)
    {
        var (review, cycle) = await GetReviewAsync(reviewId, cancellationToken);
        cycle.RequireOpenForRating();

        var performance = ScaleLevels.Parse((await GetConfigAsync(cycle.PerformanceScaleVersionId, cancellationToken)).PayloadJson, "performance");
        var potential = ScaleLevels.Parse((await GetConfigAsync(cycle.PotentialScaleVersionId, cancellationToken)).PayloadJson, "potential");
        var performanceScore = performance.ScoreOf(performanceLevel, PerformanceAxis);
        var potentialScore = potential.ScoreOf(potentialLevel, PotentialAxis);

        var layout = await GetLayoutAsync(cycle, cancellationToken);
        GridCell cell;
        if (layout.Mode == PlacementMode.Manual)
        {
            cell = column is { } c && row is { } r
                ? InsideGrid(layout, c, r)
                : throw new InvalidReviewException("This grid is placed by hand: give the column and the row.");
        }
        else
        {
            try
            {
                cell = await grids.PlaceAsync(cycle.GridVersionId, performanceScore, potentialScore, cancellationToken);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new InvalidReviewException(ex.Message.Split('(')[0].Trim());
            }
        }

        var before = review.FinalCell;
        review.Rate(performanceLevel, potentialLevel, comment, cell, clock.GetUtcNow());
        db.AddAudit(user, clock, "Rate", nameof(Review), review.Id.ToString(), confidential: true,
            beforeJson: before is { } b ? Json.Write(new { b.Column, b.Row }) : null,
            afterJson: Json.Write(new { performanceLevel, potentialLevel, cell.Column, cell.Row }));
        await SaveAsync(cancellationToken);
        return review;
    }

    public async Task<Review> CalibrateAsync(Guid reviewId, int column, int row, string? reason, CancellationToken cancellationToken)
    {
        var (review, cycle) = await GetReviewAsync(reviewId, cancellationToken);
        cycle.RequireCalibration();

        var layout = await GetLayoutAsync(cycle, cancellationToken);
        var cell = InsideGrid(layout, column, row);
        var before = review.FinalCell;
        review.Calibrate(cell, layout.Mode, user.UserId, reason ?? "", clock.GetUtcNow());
        db.AddAudit(user, clock, "Calibrate", nameof(Review), review.Id.ToString(), confidential: true,
            beforeJson: before is { } b ? Json.Write(new { b.Column, b.Row }) : null,
            afterJson: Json.Write(new { cell.Column, cell.Row, reason = review.CalibrationReason }));
        await SaveAsync(cancellationToken);
        return review;
    }

    internal async Task<ReviewCycle> GetCycleAsync(Guid cycleId, CancellationToken cancellationToken) =>
        await db.Cycles.SingleOrDefaultAsync(c => c.Id == cycleId, cancellationToken)
        ?? throw new AssessmentNotFoundException("Review cycle", cycleId);

    internal async Task<(Review Review, ReviewCycle Cycle)> GetReviewAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await db.Reviews.SingleOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
                     ?? throw new AssessmentNotFoundException("Review", reviewId);
        return (review, await GetCycleAsync(review.CycleId, cancellationToken));
    }

    internal async Task<GridLayoutDto> GetLayoutAsync(ReviewCycle cycle, CancellationToken cancellationToken) =>
        await grids.GetLayoutAsync(cycle.GridVersionId, cancellationToken)
        ?? throw new AssessmentNotFoundException("Grid version", cycle.GridVersionId);

    private static GridCell InsideGrid(GridLayoutDto layout, int column, int row) =>
        column >= 0 && column < layout.Columns && row >= 0 && row < layout.Rows
            ? new GridCell(column, row)
            : throw new InvalidReviewException($"The cell ({column}, {row}) is not on this {layout.Columns} x {layout.Rows} grid.");

    private async Task<ReviewCycle> ChangeAsync(Guid cycleId, string action, Action<ReviewCycle> change, CancellationToken cancellationToken)
    {
        var cycle = await GetCycleAsync(cycleId, cancellationToken);
        change(cycle);
        db.AddAudit(user, clock, action, nameof(ReviewCycle), cycle.Id.ToString());
        await SaveAsync(cancellationToken);
        return cycle;
    }

    private async Task<ConfigVersionDto> LatestAsync(ConfigType type, string code, CancellationToken cancellationToken) =>
        await configs.GetLatestPublishedAsync(type, code, cancellationToken)
        ?? throw new InvalidReviewException($"There is no published {type.ToString().ToLowerInvariant()} '{code}'.");

    private async Task<ConfigVersionDto> GetConfigAsync(Guid versionId, CancellationToken cancellationToken) =>
        await configs.GetAsync(versionId, cancellationToken)
        ?? throw new AssessmentNotFoundException("Config version", versionId);

    private async Task SaveAsync(CancellationToken cancellationToken, string? duplicateMessage = null)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssessmentConflictException("Someone else changed this at the same time. Reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new AssessmentConflictException(duplicateMessage ?? "That already exists.");
        }
    }
}

internal static class Json
{
    public static string Write(object value) => System.Text.Json.JsonSerializer.Serialize(value);
}
