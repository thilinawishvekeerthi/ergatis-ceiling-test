using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talent.Career.Jobs;
using Talent.Career.Paths;
using Talent.Career.Persistence;
using Talent.Career.Profiles;
using Talent.Shared.Auditing;
using Talent.Shared.Security;

namespace Talent.Career;

/// <summary>
/// Career operations: the job catalogue, career paths and employee profiles. No permission checks here (the API service does those).
/// Every change is audited. Profile changes are confidential: competency levels are personal data.
/// </summary>
internal sealed class CareerService(CareerDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<Job> CreateJobAsync(
        string code, string title, int level, string? description, IEnumerable<JobRequirement> requirements, CancellationToken cancellationToken)
    {
        var job = Job.Create(code, title, level, description, requirements);
        if (await db.Jobs.AnyAsync(j => j.Code == job.Code, cancellationToken))
        {
            throw new CareerConflictException($"A job with code {job.Code} already exists.");
        }

        db.Jobs.Add(job);
        db.AddAudit(user, clock, "CreateJob", nameof(Job), job.Id.ToString(), afterJson: Json.Write(new { job.Code, job.Title, job.Level }));
        await SaveAsync(cancellationToken, $"A job with code {job.Code} already exists.");
        return job;
    }

    public async Task<Job> UpdateJobAsync(
        Guid jobId, string title, int level, string? description, bool isActive, IEnumerable<JobRequirement> requirements, CancellationToken cancellationToken)
    {
        var job = await GetJobAsync(jobId, cancellationToken);
        job.Update(title, level, description, isActive, requirements);
        db.AddAudit(user, clock, "UpdateJob", nameof(Job), job.Id.ToString(), afterJson: Json.Write(new { job.Title, job.Level, job.IsActive }));
        await SaveAsync(cancellationToken);
        return job;
    }

    public async Task<CareerPath> AddPathAsync(Guid fromJobId, Guid toJobId, CancellationToken cancellationToken)
    {
        await GetJobAsync(fromJobId, cancellationToken);
        await GetJobAsync(toJobId, cancellationToken);
        var edges = (await db.Paths.AsNoTracking().Select(p => new { p.FromJobId, p.ToJobId }).ToListAsync(cancellationToken))
            .Select(p => (p.FromJobId, p.ToJobId));

        var path = CareerPath.Create(fromJobId, toJobId, edges);
        db.Paths.Add(path);
        db.AddAudit(user, clock, "AddPath", nameof(CareerPath), path.Id.ToString(), afterJson: Json.Write(new { fromJobId, toJobId }));
        await SaveAsync(cancellationToken, "That career path already exists.");
        return path;
    }

    public async Task RemovePathAsync(Guid pathId, CancellationToken cancellationToken)
    {
        var path = await db.Paths.SingleOrDefaultAsync(p => p.Id == pathId, cancellationToken)
                   ?? throw new CareerNotFoundException("Career path", pathId);
        db.Paths.Remove(path);
        db.AddAudit(user, clock, "RemovePath", nameof(CareerPath), path.Id.ToString(), beforeJson: Json.Write(new { path.FromJobId, path.ToJobId }));
        await SaveAsync(cancellationToken);
    }

    public async Task<EmployeeProfile> ReplaceProfileAsync(
        Guid employeeId, Guid? currentJobId, IEnumerable<(string Code, int Level)> competencies, CancellationToken cancellationToken)
    {
        if (currentJobId is { } jobId)
        {
            await GetJobAsync(jobId, cancellationToken);
        }

        var profile = await db.Profiles.Include(p => p.Competencies).SingleOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken);
        var isNew = profile is null;
        profile ??= new EmployeeProfile(employeeId);

        profile.Replace(currentJobId, competencies, clock.GetUtcNow());
        if (isNew)
        {
            db.Profiles.Add(profile);
        }

        db.AddAudit(user, clock, "ReplaceProfile", nameof(EmployeeProfile), employeeId.ToString(), confidential: true,
            afterJson: Json.Write(new { currentJobId, competencies = profile.Levels }));
        await SaveAsync(cancellationToken, "This profile was changed at the same time. Reload and try again.");
        return profile;
    }

    internal async Task<Job> GetJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        await db.Jobs.Include(j => j.Requirements).SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken)
        ?? throw new CareerNotFoundException("Job", jobId);

    private async Task SaveAsync(CancellationToken cancellationToken, string? duplicateMessage = null)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CareerConflictException("Someone else changed this at the same time. Reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new CareerConflictException(duplicateMessage ?? "That already exists.");
        }
    }
}

internal static class Json
{
    public static string Write(object value) => System.Text.Json.JsonSerializer.Serialize(value);
}
