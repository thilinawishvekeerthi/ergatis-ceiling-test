using FluentValidation;
using Talent.Assessment.Contracts;
using Microsoft.EntityFrameworkCore;
using Talent.Assessment.Cycles;
using Talent.Assessment.Persistence;
using Talent.Assessment.Reviews;
using Talent.Config.Contracts;
using Talent.Integration.Contracts;
using Talent.Security.Contracts;
using Talent.Shared.Errors;

namespace Talent.Assessment.Api;

/// <summary>
/// The Assessment API rules (ADR-07): the permission matrix first (403), then the data scope, then the operation, then masking.
/// Resources: <c>assessment:Cycle</c> (View, Edit to create or assign, Approve to open, calibrate and close),
/// <c>assessment:Review</c> (View, Edit to rate, Calibrate) and <c>assessment:Board</c> (View).
/// </summary>
internal sealed class AssessmentApiService(
    AssessmentDbContext db,
    AssessmentService service,
    IAccessGuard guard,
    IDataScopeService scopes,
    IFieldMasker masker,
    ISecurityUser user,
    IEmployeeReader employees,
    IConfigVersionReader configs,
    IValidator<CreateCycleRequest> createValidator,
    IValidator<AssignReviewRequest> assignValidator,
    IValidator<RateRequest> rateValidator,
    IValidator<CalibrateRequest> calibrateValidator)
{
    public const string CycleResource = "assessment:Cycle";
    public const string ReviewResource = "assessment:Review";
    public const string BoardResource = "assessment:Board";

    public async Task<IReadOnlyList<CycleResponse>> ListCyclesAsync(CancellationToken cancellationToken)
    {
        var grant = await guard.RequireAsync(CycleResource, PermissionAction.View, null, cancellationToken);
        var cycles = await db.Cycles.OrderByDescending(c => c.PeriodStart).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        var counts = await CountsAsync(cancellationToken);
        return [.. cycles.Select(c => masker.Mask(ToResponse(c, counts), grant))];
    }

    public async Task<CycleResponse> GetCycleAsync(Guid cycleId, CancellationToken cancellationToken)
    {
        var grant = await RequireCycleAsync(PermissionAction.View, cycleId, cancellationToken);
        var cycle = await service.GetCycleAsync(cycleId, cancellationToken);
        return masker.Mask(ToResponse(cycle, await CountsAsync(cancellationToken)), grant);
    }

    public async Task<CycleResponse> CreateCycleAsync(CreateCycleRequest request, CancellationToken cancellationToken)
    {
        Validate(createValidator, request);
        var grant = await guard.RequireAsync(CycleResource, PermissionAction.Edit, null, cancellationToken);
        var cycle = await service.CreateCycleAsync(
            request.Name!, request.PeriodStart!.Value, request.PeriodEnd!.Value,
            request.PerformanceScaleCode ?? "PERFORMANCE_5", request.PotentialScaleCode ?? "POTENTIAL_3", request.GridCode ?? "NINE_BOX",
            cancellationToken);
        return masker.Mask(ToResponse(cycle, new Dictionary<Guid, (int, int)>()), grant);
    }

    public Task<CycleResponse> OpenCycleAsync(Guid cycleId, CancellationToken cancellationToken) =>
        MoveCycleAsync(cycleId, service.OpenCycleAsync, cancellationToken);

    public Task<CycleResponse> StartCalibrationAsync(Guid cycleId, CancellationToken cancellationToken) =>
        MoveCycleAsync(cycleId, service.StartCalibrationAsync, cancellationToken);

    public Task<CycleResponse> CloseCycleAsync(Guid cycleId, CancellationToken cancellationToken) =>
        MoveCycleAsync(cycleId, service.CloseCycleAsync, cancellationToken);

    public async Task<ReviewResponse> AssignAsync(Guid cycleId, AssignReviewRequest request, CancellationToken cancellationToken)
    {
        Validate(assignValidator, request);
        await RequireCycleAsync(PermissionAction.Edit, cycleId, cancellationToken);
        var reviewGrant = await guard.RequireGrantAsync(ReviewResource, PermissionAction.View, cancellationToken);
        var review = await service.AssignAsync(cycleId, request.EmployeeId!.Value, request.ReviewerId!.Value, cancellationToken);
        return (await ToResponsesAsync([review], await service.GetCycleAsync(cycleId, cancellationToken), reviewGrant, cancellationToken))[0];
    }

    public async Task<IReadOnlyList<ReviewResponse>> ListReviewsAsync(Guid cycleId, CancellationToken cancellationToken)
    {
        var grant = await guard.RequireGrantAsync(ReviewResource, PermissionAction.View, cancellationToken);
        var cycle = await service.GetCycleAsync(cycleId, cancellationToken);
        var inScope = await scopes.FilterAsync<Review>(grant, r => r.EmployeeId, cancellationToken);
        var rows = await db.Reviews.Where(r => r.CycleId == cycleId).Where(inScope).ToListAsync(cancellationToken);
        return await ToResponsesAsync(rows, cycle, grant, cancellationToken);
    }

    public async Task<ReviewResponse> GetReviewAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var (review, cycle) = await FindReviewAsync(reviewId, cancellationToken);
        var grant = await guard.RequireAsync(ReviewResource, PermissionAction.View, review.EmployeeId, cancellationToken);
        return (await ToResponsesAsync([review], cycle, grant, cancellationToken))[0];
    }

    public async Task<ReviewResponse> RateAsync(Guid reviewId, RateRequest request, CancellationToken cancellationToken)
    {
        Validate(rateValidator, request);
        var (review, _) = await FindReviewAsync(reviewId, cancellationToken);
        var grant = await guard.RequireAsync(ReviewResource, PermissionAction.Edit, review.EmployeeId, cancellationToken);
        if (review.ReviewerId != user.EmployeeId)
        {
            throw new ForbiddenException("Only the assigned reviewer can rate this employee.");
        }

        var rated = await service.RateAsync(
            reviewId, request.PerformanceLevel!.Value, request.PotentialLevel!.Value, request.Comment, request.Column, request.Row, cancellationToken);
        return (await ToResponsesAsync([rated], await service.GetCycleAsync(rated.CycleId, cancellationToken), grant, cancellationToken))[0];
    }

    public async Task<ReviewResponse> CalibrateAsync(Guid reviewId, CalibrateRequest request, CancellationToken cancellationToken)
    {
        Validate(calibrateValidator, request);
        var (review, _) = await FindReviewAsync(reviewId, cancellationToken);
        var grant = await guard.RequireAsync(ReviewResource, PermissionAction.Calibrate, review.EmployeeId, cancellationToken);
        var calibrated = await service.CalibrateAsync(reviewId, request.Column!.Value, request.Row!.Value, request.Reason, cancellationToken);
        return (await ToResponsesAsync([calibrated], await service.GetCycleAsync(calibrated.CycleId, cancellationToken), grant, cancellationToken))[0];
    }

    public async Task<BoardResponse> GetBoardAsync(Guid cycleId, CancellationToken cancellationToken)
    {
        var grant = await guard.RequireGrantAsync(BoardResource, PermissionAction.View, cancellationToken);
        if (!grant.TalentConfidential)
        {
            throw new ForbiddenException("You do not have permission to see the grid.");
        }

        var cycle = await service.GetCycleAsync(cycleId, cancellationToken);
        var layout = await service.GetLayoutAsync(cycle, cancellationToken);
        var inScope = await scopes.FilterAsync<Review>(grant, r => r.EmployeeId, cancellationToken);
        var rows = await db.Reviews
            .Where(r => r.CycleId == cycleId && r.Status == ReviewStatus.Rated && r.EmployeeId != user.EmployeeId)
            .Where(inScope)
            .ToListAsync(cancellationToken);
        var names = await NamesAsync(rows, cancellationToken);

        var people = rows
            .Where(r => r.FinalCell is not null)
            .Select(r => new BoardPerson(
                r.Id, r.EmployeeId, names.GetValueOrDefault(r.EmployeeId), r.FinalColumn!.Value, r.FinalRow!.Value,
                r.IsCalibrated, r.ProposedColumn, r.ProposedRow))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
        return new BoardResponse(cycle.Id, cycle.Name, cycle.Status.ToString(), layout.Mode.ToString(), layout, people);
    }

    private async Task<CycleResponse> MoveCycleAsync(
        Guid cycleId, Func<Guid, CancellationToken, Task<ReviewCycle>> move, CancellationToken cancellationToken)
    {
        var grant = await RequireCycleAsync(PermissionAction.Approve, cycleId, cancellationToken);
        var cycle = await move(cycleId, cancellationToken);
        return masker.Mask(ToResponse(cycle, await CountsAsync(cancellationToken)), grant);
    }

    private async Task<AccessGrant> RequireCycleAsync(PermissionAction action, Guid cycleId, CancellationToken cancellationToken)
    {
        if (!await db.Cycles.AnyAsync(c => c.Id == cycleId, cancellationToken))
        {
            // No rights at all gives 403, so ids cannot be probed.
            await guard.RequireAnyAsync("assessment:", action, cancellationToken);
            throw new AssessmentNotFoundException("Review cycle", cycleId);
        }

        return await guard.RequireAsync(CycleResource, action, null, cancellationToken);
    }

    private async Task<(Review Review, ReviewCycle Cycle)> FindReviewAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        try
        {
            return await service.GetReviewAsync(reviewId, cancellationToken);
        }
        catch (AssessmentNotFoundException)
        {
            await guard.RequireAnyAsync("assessment:", PermissionAction.View, cancellationToken);
            throw;
        }
    }

    private async Task<Dictionary<Guid, (int Reviews, int Rated)>> CountsAsync(CancellationToken cancellationToken) =>
        (await db.Reviews.GroupBy(r => r.CycleId)
            .Select(g => new { g.Key, Total = g.Count(), Rated = g.Count(r => r.Status == ReviewStatus.Rated) })
            .ToListAsync(cancellationToken))
        .ToDictionary(x => x.Key, x => (x.Total, x.Rated));

    private static CycleResponse ToResponse(ReviewCycle c, IReadOnlyDictionary<Guid, (int Reviews, int Rated)> counts)
    {
        var (reviews, rated) = counts.GetValueOrDefault(c.Id);
        return new CycleResponse(
            c.Id, c.Name, c.PeriodStart, c.PeriodEnd, c.Status.ToString(), c.PerformanceScaleVersionId, c.PotentialScaleVersionId,
            c.GridVersionId, c.OpenedAt, c.ClosedAt, reviews, rated);
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Review> reviews, CancellationToken cancellationToken)
    {
        var ids = reviews.Select(r => r.EmployeeId).Distinct().ToList();
        var found = ids.Count == 0 ? [] : await employees.GetManyAsync(ids, cancellationToken);
        return found.ToDictionary(e => e.Id, e => $"{e.FirstName} {e.LastName}");
    }

    private async Task<IReadOnlyList<ReviewResponse>> ToResponsesAsync(
        IReadOnlyCollection<Review> reviews, ReviewCycle cycle, AccessGrant grant, CancellationToken cancellationToken)
    {
        var names = await NamesAsync(reviews, cancellationToken);
        var performanceJson = (await configs.GetAsync(cycle.PerformanceScaleVersionId, cancellationToken))?.PayloadJson;
        var scale = performanceJson is null ? null : ScaleLevels.Parse(performanceJson, "performance");

        return [.. reviews.OrderBy(r => names.GetValueOrDefault(r.EmployeeId), StringComparer.Ordinal).Select(r => masker.Mask(
            new ReviewResponse(
                r.Id, r.CycleId, r.EmployeeId, names.GetValueOrDefault(r.EmployeeId), r.ReviewerId, r.Status.ToString(),
                r.PerformanceLevel, r.PerformanceLevel is { } p ? scale?.LabelOf(p) : null, r.PotentialLevel, r.Comment, r.RatedAt,
                r.ProposedColumn, r.ProposedRow, r.FinalColumn, r.FinalRow, r.CalibratedBy, r.CalibrationReason),
            grant,
            isOwnRecord: r.EmployeeId == user.EmployeeId))];
    }

    private static void Validate<T>(IValidator<T> validator, T value)
    {
        var result = validator.Validate(value);
        if (!result.IsValid)
        {
            throw new ValidationFailedException(result.Errors
                .GroupBy(e => JsonName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
    }

    private static string JsonName(string property) =>
        string.IsNullOrEmpty(property) ? property : char.ToLowerInvariant(property[0]) + property[1..];
}
