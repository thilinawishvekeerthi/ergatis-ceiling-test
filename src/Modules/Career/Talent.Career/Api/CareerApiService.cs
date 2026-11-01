using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Talent.Career.Jobs;
using Talent.Career.Matching;
using Talent.Career.Paths;
using Talent.Career.Persistence;
using Talent.Security.Contracts;
using Talent.Shared.Errors;

namespace Talent.Career.Api;

/// <summary>
/// The Career API rules (ADR-07): the permission matrix first (403), then the data scope, then the operation, then masking.
/// Resources: <c>career:Job</c> and <c>career:Path</c> (organisation-wide, View and Edit) and <c>career:Profile</c> (per employee).
/// </summary>
internal sealed class CareerApiService(
    CareerDbContext db,
    CareerService service,
    IAccessGuard guard,
    IFieldMasker masker,
    ISecurityUser user,
    IValidator<JobRequest> jobValidator,
    IValidator<AddPathRequest> pathValidator,
    IValidator<ProfileRequest> profileValidator)
{
    public const string JobResource = "career:Job";
    public const string PathResource = "career:Path";
    public const string ProfileResource = "career:Profile";

    /// <summary>How many career moves ahead the Career Centre looks.</summary>
    public const int MaxSteps = 3;

    public async Task<IReadOnlyList<JobResponse>> ListJobsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var grant = await guard.RequireAsync(JobResource, PermissionAction.View, null, cancellationToken);
        var jobs = await db.Jobs.Include(j => j.Requirements)
            .Where(j => includeInactive || j.IsActive)
            .OrderBy(j => j.Level).ThenBy(j => j.Title)
            .ToListAsync(cancellationToken);
        return [.. jobs.Select(j => masker.Mask(JobResponse.From(j), grant))];
    }

    public async Task<JobResponse> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var grant = await guard.RequireAsync(JobResource, PermissionAction.View, null, cancellationToken);
        return masker.Mask(JobResponse.From(await FindJobAsync(jobId, PermissionAction.View, cancellationToken)), grant);
    }

    public async Task<JobResponse> CreateJobAsync(JobRequest request, CancellationToken cancellationToken)
    {
        Validate(jobValidator, request);
        if (string.IsNullOrEmpty(request.Code))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["code"] = ["A job code is required."] });
        }

        var grant = await guard.RequireAsync(JobResource, PermissionAction.Edit, null, cancellationToken);
        var job = await service.CreateJobAsync(request.Code, request.Title!, request.Level!.Value, request.Description, Requirements(request), cancellationToken);
        return masker.Mask(JobResponse.From(job), grant);
    }

    public async Task<JobResponse> UpdateJobAsync(Guid jobId, JobRequest request, CancellationToken cancellationToken)
    {
        Validate(jobValidator, request);
        var grant = await guard.RequireAsync(JobResource, PermissionAction.Edit, null, cancellationToken);
        await FindJobAsync(jobId, PermissionAction.Edit, cancellationToken);
        var job = await service.UpdateJobAsync(
            jobId, request.Title!, request.Level!.Value, request.Description, request.IsActive ?? true, Requirements(request), cancellationToken);
        return masker.Mask(JobResponse.From(job), grant);
    }

    public async Task<IReadOnlyList<PathResponse>> ListPathsAsync(CancellationToken cancellationToken)
    {
        var grant = await guard.RequireAsync(PathResource, PermissionAction.View, null, cancellationToken);
        var rows = await (from p in db.Paths
                          join f in db.Jobs on p.FromJobId equals f.Id
                          join t in db.Jobs on p.ToJobId equals t.Id
                          orderby f.Level, f.Title, t.Level, t.Title
                          select new PathResponse(p.Id, p.FromJobId, f.Title, p.ToJobId, t.Title))
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => masker.Mask(r, grant))];
    }

    public async Task<PathResponse> AddPathAsync(AddPathRequest request, CancellationToken cancellationToken)
    {
        Validate(pathValidator, request);
        var grant = await guard.RequireAsync(PathResource, PermissionAction.Edit, null, cancellationToken);
        var path = await service.AddPathAsync(request.FromJobId!.Value, request.ToJobId!.Value, cancellationToken);
        var from = await service.GetJobAsync(path.FromJobId, cancellationToken);
        var to = await service.GetJobAsync(path.ToJobId, cancellationToken);
        return masker.Mask(new PathResponse(path.Id, path.FromJobId, from.Title, path.ToJobId, to.Title), grant);
    }

    public async Task RemovePathAsync(Guid pathId, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(PathResource, PermissionAction.Edit, null, cancellationToken);
        await service.RemovePathAsync(pathId, cancellationToken);
    }

    public async Task<CentreResponse> ReplaceProfileAsync(Guid employeeId, ProfileRequest request, CancellationToken cancellationToken)
    {
        Validate(profileValidator, request);
        var grant = await guard.RequireAsync(ProfileResource, PermissionAction.Edit, employeeId, cancellationToken);
        await service.ReplaceProfileAsync(
            employeeId, request.CurrentJobId, request.Competencies!.Select(c => (c.CompetencyCode!, c.Level!.Value)), cancellationToken);
        return await BuildCentreAsync(employeeId, grant, cancellationToken);
    }

    /// <summary>The Career Centre of the signed-in employee.</summary>
    public Task<CentreResponse> GetMyCentreAsync(CancellationToken cancellationToken) =>
        GetCentreAsync(user.EmployeeId ?? throw new ForbiddenException("Your login is not linked to an employee."), cancellationToken);

    public async Task<CentreResponse> GetCentreAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var grant = await guard.RequireAsync(ProfileResource, PermissionAction.View, employeeId, cancellationToken);
        return await BuildCentreAsync(employeeId, grant, cancellationToken);
    }

    private async Task<CentreResponse> BuildCentreAsync(Guid employeeId, AccessGrant grant, CancellationToken cancellationToken)
    {
        if (!grant.CanRead(FieldClass.Internal, isOwnRecord: employeeId == user.EmployeeId))
        {
            throw new ForbiddenException("You do not have permission to see competency levels.");
        }

        db.ChangeTracker.Clear();
        var profile = await db.Profiles.AsNoTracking().Include(p => p.Competencies).SingleOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken);
        var levels = profile?.Levels ?? new Dictionary<string, int>();
        var jobs = await db.Jobs.AsNoTracking().Include(j => j.Requirements).Where(j => j.IsActive).ToListAsync(cancellationToken);

        var current = profile?.CurrentJobId is { } currentId ? await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == currentId, cancellationToken) : null;
        Dictionary<Guid, int?> candidates;
        if (current is null)
        {
            candidates = jobs.ToDictionary(j => j.Id, _ => (int?)null);
        }
        else
        {
            var edges = (await db.Paths.AsNoTracking().Select(p => new { p.FromJobId, p.ToJobId }).ToListAsync(cancellationToken))
                .Select(p => (p.FromJobId, p.ToJobId));
            candidates = CareerGraph.Reachable(edges, current.Id, MaxSteps).ToDictionary(kv => kv.Key, kv => (int?)kv.Value);
        }

        var opportunities = jobs
            .Where(j => candidates.ContainsKey(j.Id) && j.Id != current?.Id)
            .Select(j =>
            {
                var match = MatchCalculator.Calculate(j.Requirements, levels);
                return new Opportunity(
                    Summary(j), candidates[j.Id], match.Percent,
                    [.. match.Requirements.Select(r => new RequirementProgress(r.CompetencyCode, r.RequiredLevel, r.CurrentLevel, r.Weight, r.IsMet, r.Gap))]);
            })
            .OrderBy(o => o.Steps ?? int.MaxValue).ThenByDescending(o => o.MatchPercent).ThenBy(o => o.Job.Title, StringComparer.Ordinal)
            .ToList();

        return new CentreResponse(
            employeeId,
            current is null ? null : Summary(current),
            [.. levels.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => new CompetencyLevelDto(l.Key, l.Value))],
            opportunities);
    }

    private static JobSummary Summary(Job j) => new(j.Id, j.Code, j.Title, j.Level);

    private static IEnumerable<JobRequirement> Requirements(JobRequest request) =>
        request.Requirements!.Select(r => new JobRequirement(r.CompetencyCode!, r.RequiredLevel!.Value, r.Weight ?? 1));

    private async Task<Job> FindJobAsync(Guid jobId, PermissionAction action, CancellationToken cancellationToken)
    {
        try
        {
            return await service.GetJobAsync(jobId, cancellationToken);
        }
        catch (CareerNotFoundException)
        {
            // No rights at all gives 403, so ids cannot be probed.
            await guard.RequireAnyAsync("career:", action, cancellationToken);
            throw;
        }
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
