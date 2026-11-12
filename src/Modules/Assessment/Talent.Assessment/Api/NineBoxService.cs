using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Talent.Assessment.NineBox;
using Talent.Assessment.Persistence;
using Talent.Config.Contracts;
using Talent.Integration.Contracts;
using Talent.Security.Contracts;
using Talent.Shared.Auditing;
using Talent.Shared.Errors;
using Talent.Shared.Security;

namespace Talent.Assessment.Api;

internal sealed class EmployeeNotFoundException(Guid employeeId)
    : TalentProblemException(404, $"Employee {employeeId} was not found.");

/// <summary>
/// The nine-box use cases. Order in every call: permission (403), input (400), then the work.
/// Reads need View on <c>assessment:NineBox</c>; an evaluation needs Edit on the evaluated employee.
/// </summary>
internal sealed class NineBoxService(
    AssessmentDbContext db,
    NineBoxConfigProvider config,
    IConfigVersionLocker locker,
    IAccessGuard guard,
    IFieldMasker masker,
    IEmployeeReader employees,
    IPositionReader positions,
    IOrgUnitReader orgUnits,
    ICurrentUser user,
    TimeProvider clock)
{
    public const string Resource = "assessment:NineBox";
    public const int MaxPageSize = 100;

    public async Task<NineBoxGridResponse> GetGridAsync(CancellationToken cancellationToken)
    {
        await guard.RequireAsync(Resource, PermissionAction.View, null, cancellationToken);
        return NineBoxGridResponse.From(await config.GetCurrentAsync(cancellationToken));
    }

    /// <summary>
    /// One page of employees with their latest placement. A fixed number of queries per page whatever its size:
    /// employees, their positions, their units, their latest evaluations. No query per row.
    /// </summary>
    public async Task<PagedResult<EmployeePlacementRow>> ListEmployeesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var grant = await guard.RequireAsync(Resource, PermissionAction.View, null, cancellationToken);
        var result = await employees.ListAsync(null, page, Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken);

        var employeeIds = result.Items.Select(e => e.Id).ToList();
        var titles = (await positions.GetManyAsync([.. result.Items.Select(e => e.PositionId).OfType<Guid>().Distinct()], cancellationToken))
            .ToDictionary(p => p.Id, p => p.Title);
        var units = (await orgUnits.GetManyAsync([.. result.Items.Select(e => e.OrgUnitId).OfType<Guid>().Distinct()], cancellationToken))
            .ToDictionary(o => o.Id, o => o.Name);
        var latest = await (
                from l in db.LatestPlacements
                where employeeIds.Contains(l.EmployeeId)
                join e in db.Evaluations on l.EvaluationId equals e.Id
                select e)
            .AsNoTracking()
            .ToDictionaryAsync(e => e.EmployeeId, cancellationToken);

        var rows = result.Items.Select(e => masker.Mask(
            new EmployeePlacementRow(
                e.Id,
                e.EmployeeNumber,
                $"{e.FirstName} {e.LastName}",
                e.PositionId is { } p ? titles.GetValueOrDefault(p) : null,
                e.OrgUnitId is { } o ? units.GetValueOrDefault(o) : null,
                latest.TryGetValue(e.Id, out var evaluation) ? EvaluationResponse.From(evaluation) : null),
            grant)).ToList();

        return new PagedResult<EmployeePlacementRow>(rows, result.Page, result.PageSize, result.TotalCount);
    }

    /// <summary>All evaluations of one employee, newest first.</summary>
    public async Task<PagedResult<EvaluationResponse>> HistoryAsync(Guid employeeId, int page, int pageSize, CancellationToken cancellationToken)
    {
        RequireConfidential(await guard.RequireAsync(Resource, PermissionAction.View, employeeId, cancellationToken));
        _ = await employees.GetAsync(employeeId, cancellationToken) ?? throw new EmployeeNotFoundException(employeeId);

        var p = Math.Max(page, 1);
        var size = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = db.Evaluations.AsNoTracking().Where(e => e.EmployeeId == employeeId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.EvaluatedAt).ThenByDescending(e => e.Id)
            .Skip((p - 1) * size).Take(size)
            .ToListAsync(cancellationToken);

        return new PagedResult<EvaluationResponse>([.. items.Select(EvaluationResponse.From)], p, size, total);
    }

    public async Task<EvaluationResponse> EvaluateAsync(EvaluateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var employeeId = request.EmployeeId is { } id && id != Guid.Empty
            ? id
            : throw new ValidationFailedException(new Dictionary<string, string[]> { ["employeeId"] = ["Choose an employee."] });

        RequireConfidential(await guard.RequireAsync(Resource, PermissionAction.Edit, employeeId, cancellationToken));
        RequireLevels(request);

        _ = await employees.GetAsync(employeeId, cancellationToken) ?? throw new EmployeeNotFoundException(employeeId);
        var current = await config.GetCurrentAsync(cancellationToken);
        var placement = NineBoxRules.Place(request.Performance!.Value, request.Potential!.Value, current.Scheme);

        // ADR-02: a config version becomes immutable once a record uses it.
        await locker.LockAsync(current.GridVersionId, cancellationToken);

        var evaluation = NineBoxEvaluation.Record(employeeId, current.GridVersionId, placement, user.UserId, clock.GetUtcNow());

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Evaluations.Add(evaluation);
        db.AddAudit(user, clock, "NineBoxEvaluate", nameof(NineBoxEvaluation), evaluation.Id.ToString(), confidential: true,
            afterJson: JsonSerializer.Serialize(new { employeeId, placement.Performance, placement.Potential, placement.BoxNumber, placement.TalentScore }));
        await db.SaveChangesAsync(cancellationToken);

        // Latest wins by evaluation time, decided in one statement, so two reviewers saving at once cannot leave an older pointer.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO assessment.latest_nine_box_placements (employee_id, evaluation_id, evaluated_at)
            VALUES ({employeeId}, {evaluation.Id}, {evaluation.EvaluatedAt})
            ON CONFLICT (employee_id) DO UPDATE
            SET evaluation_id = EXCLUDED.evaluation_id, evaluated_at = EXCLUDED.evaluated_at
            WHERE latest_nine_box_placements.evaluated_at <= EXCLUDED.evaluated_at
            """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return EvaluationResponse.From(evaluation);
    }

    private static void RequireConfidential(AccessGrant grant)
    {
        if (!grant.CanRead(FieldClass.TalentConfidential))
        {
            throw new ForbiddenException("Nine-box results are talent-confidential.");
        }
    }

    private static void RequireLevels(EvaluateRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Performance is null)
        {
            errors["performance"] = ["Choose a performance level from 1 to 3."];
        }

        if (request.Potential is null)
        {
            errors["potential"] = ["Choose a potential level from 1 to 3."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }
    }
}
