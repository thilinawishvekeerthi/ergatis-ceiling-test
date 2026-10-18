using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TalentHub.Api.Data;
using TalentHub.Api.Domain;

namespace TalentHub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController(TalentHubDbContext db) : ControllerBase
{
    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

    // ---- Job families / jobs / alternative career paths (blueprint step 1) ----

    [HttpGet("job-families")]
    public async Task<IActionResult> JobFamilies() =>
        Ok(await db.JobFamilies.Include(f => f.Jobs).AsNoTracking().ToListAsync());

    public record JobFamilyDto(string Name, string? Description);
    public record JobDto(string Title);

    [HttpPost("job-families")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> CreateJobFamily(JobFamilyDto dto)
    {
        if (await db.JobFamilies.AnyAsync(f => f.Name == dto.Name))
            return Conflict(new { error = "Job family name already exists." });
        var fam = new JobFamily { Name = dto.Name, Description = dto.Description };
        db.JobFamilies.Add(fam);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(JobFamilies), new { id = fam.Id }, fam);
    }

    [HttpPut("job-families/{id:int}/jobs")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> AssignJobs(int id, [FromBody] JobDto[] jobs)
    {
        var fam = await db.JobFamilies.Include(f => f.Jobs).FirstOrDefaultAsync(f => f.Id == id);
        if (fam is null) return NotFound();

        var incoming = jobs.Select(j => j.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var current = fam.Jobs.Select(j => j.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var title in incoming.Except(current)) db.Jobs.Add(new Job { JobFamilyId = id, Title = title });
        var removed = fam.Jobs.Where(j => !incoming.Contains(j.Title, StringComparer.OrdinalIgnoreCase)).ToList();
        db.Jobs.RemoveRange(removed);
        await db.SaveChangesAsync();
        return Ok(fam.Jobs);
    }

    public record AltPathDto(int FromJobFamilyId, int ToJobFamilyId, string? Notes);

    [HttpGet("alternative-career-paths")]
    public async Task<IActionResult> AltPaths() =>
        Ok(await db.AlternativeCareerPaths
            .Include(p => p.FromJobFamily).Include(p => p.ToJobFamily).AsNoTracking().ToListAsync());

    [HttpPost("alternative-career-paths")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> AddAltPath(AltPathDto dto)
    {
        if (dto.FromJobFamilyId == dto.ToJobFamilyId)
            return BadRequest(new { error = "From and To job families must differ." });
        if (!await db.JobFamilies.AnyAsync(f => f.Id == dto.FromJobFamilyId) ||
            !await db.JobFamilies.AnyAsync(f => f.Id == dto.ToJobFamilyId))
            return NotFound(new { error = "Job family not found." });
        if (await db.AlternativeCareerPaths.AnyAsync(p => p.FromJobFamilyId == dto.FromJobFamilyId && p.ToJobFamilyId == dto.ToJobFamilyId))
            return Conflict(new { error = "Path already exists." });
        var path = new AlternativeCareerPath { FromJobFamilyId = dto.FromJobFamilyId, ToJobFamilyId = dto.ToJobFamilyId, Notes = dto.Notes };
        db.AlternativeCareerPaths.Add(path);
        await db.SaveChangesAsync();
        return Ok(path);
    }

    // ---- Scales ----

    public record ScaleDto(string Name, List<ScaleLevelDto> Levels);
    public record ScaleLevelDto(string Label, int Value);

    [HttpGet("scales")]
    public async Task<IActionResult> Scales() =>
        Ok(await db.Scales.Include(s => s.Levels).AsNoTracking().ToListAsync());

    [HttpPost("scales")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> CreateScale(ScaleDto dto)
    {
        if (dto.Levels.Select(l => l.Value).Distinct().Count() != dto.Levels.Count)
            return BadRequest(new { error = "Scale level values must be unique." });
        var scale = new Scale { Name = dto.Name };
        foreach (var l in dto.Levels) scale.Levels.Add(new ScaleLevel { Label = l.Label, Value = l.Value });
        db.Scales.Add(scale);
        await db.SaveChangesAsync();
        return Ok(scale);
    }

    // ---- Talent review cycles (steps 3, 5) ----

    public record CycleDto(string Name, DateOnly StartDate, DateOnly EndDate, int? ScaleId, List<CycleCompetencyDto> Competencies);
    public record CycleCompetencyDto(string Name, string? Description, int? Weight);

    [HttpGet("cycles")]
    public async Task<IActionResult> Cycles() =>
        Ok(await db.TalentReviewCycles.Include(c => c.Competencies).AsNoTracking().ToListAsync());

    [HttpPost("cycles")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> CreateCycle(CycleDto dto)
    {
        if (dto.EndDate < dto.StartDate) return BadRequest(new { error = "EndDate must be on or after StartDate." });
        if (dto.ScaleId is int sid && !await db.Scales.AnyAsync(s => s.Id == sid))
            return NotFound(new { error = "Scale not found." });
        var cycle = new TalentReviewCycle { Name = dto.Name, StartDate = dto.StartDate, EndDate = dto.EndDate, ScaleId = dto.ScaleId };
        foreach (var c in dto.Competencies) cycle.Competencies.Add(new CycleCompetency { Name = c.Name, Description = c.Description, Weight = c.Weight });
        db.TalentReviewCycles.Add(cycle);
        await db.SaveChangesAsync();
        return Ok(cycle);
    }

    [HttpPost("cycles/{id:int}/close")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> CloseCycle(int id)
    {
        var cycle = await db.TalentReviewCycles.FindAsync(id);
        if (cycle is null) return NotFound();
        cycle.IsOpen = false;
        await db.SaveChangesAsync();
        return Ok(cycle);
    }

    // ---- Review boards & meetings (steps 6, 9) ----

    public record BoardDto(string Name, List<string> MemberUsernames, string HeadUsername);
    public record MeetingDto(int CycleId, DateTime ScheduledAt, List<string> MemberUsernames, string HeadUsername);

    [HttpGet("boards")]
    public async Task<IActionResult> Boards() =>
        Ok(await db.ReviewBoards.Include(b => b.Members).ThenInclude(m => m.Member).AsNoTracking().ToListAsync());

    [HttpPost("boards")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> CreateBoard(BoardDto dto)
    {
        if (dto.MemberUsernames.Count == 0) return BadRequest(new { error = "At least one member is required." });
        if (!dto.MemberUsernames.Contains(dto.HeadUsername, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Head must be a member of the board." });
        var memberIds = await ResolveUserIds(dto.MemberUsernames);
        if (memberIds is null) return NotFound(new { error = "One or more members not found." });
        var headId = await ResolveUserId(dto.HeadUsername);
        if (headId is null) return NotFound(new { error = "Head not found." });

        var board = new ReviewBoard { Name = dto.Name, HeadEmployeeId = headId };
        foreach (var m in memberIds) board.Members.Add(new ReviewBoardMember { MemberEmployeeId = m });
        db.ReviewBoards.Add(board);
        await db.SaveChangesAsync();
        return Ok(board);
    }

    [HttpPost("meetings")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> ScheduleMeeting(MeetingDto dto)
    {
        if (!await db.TalentReviewCycles.AnyAsync(c => c.Id == dto.CycleId)) return NotFound(new { error = "Cycle not found." });
        var memberIds = await ResolveUserIds(dto.MemberUsernames);
        if (memberIds is null) return NotFound(new { error = "One or more members not found." });
        var headId = await ResolveUserId(dto.HeadUsername);
        if (headId is null) return NotFound(new { error = "Head not found." });
        var meeting = new ReviewMeeting { CycleId = dto.CycleId, ScheduledAt = dto.ScheduledAt, HeadEmployeeId = headId };
        foreach (var m in memberIds) meeting.Members.Add(new ReviewMeetingMember { MemberEmployeeId = m });
        db.ReviewMeetings.Add(meeting);
        await db.SaveChangesAsync();
        return Ok(meeting);
    }

    [HttpGet("meetings")]
    public async Task<IActionResult> Meetings() =>
        Ok(await db.ReviewMeetings.Include(m => m.Members).ThenInclude(x => x.Member).AsNoTracking().ToListAsync());

    // ---- Development plan config (step 13) ----

    [HttpGet("development-plan-config")]
    public async Task<IActionResult> GetDevConfig()
    {
        var cfg = await db.DevelopmentPlanConfigs.OrderBy(c => c.Id).FirstOrDefaultAsync();
        return cfg is null ? NotFound() : Ok(cfg);
    }

    public record DevConfigDto(ReviewFrequency ReviewFrequency, int ReviewPeriodMonths, OverallRatingMethod OverallRatingMethod);

    [HttpPut("development-plan-config")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> UpdateDevConfig(DevConfigDto dto)
    {
        if (dto.ReviewPeriodMonths is < 1 or > 36) return BadRequest(new { error = "Review period must be 1-36 months." });
        var cfg = await db.DevelopmentPlanConfigs.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (cfg is null) { cfg = new DevelopmentPlanConfig(); db.DevelopmentPlanConfigs.Add(cfg); }
        cfg.ReviewFrequency = dto.ReviewFrequency;
        cfg.ReviewPeriodMonths = dto.ReviewPeriodMonths;
        cfg.OverallRatingMethod = dto.OverallRatingMethod;
        await db.SaveChangesAsync();
        return Ok(cfg);
    }

    // ---- Leadership candidates (step 4) ----

    [HttpGet("leadership-candidates")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> LeadershipCandidates()
    {
        var list = await db.LeadershipCandidates
            .Include(c => c.Candidate)
            .OrderByDescending(c => c.Score).AsNoTracking().ToListAsync();
        return Ok(list);
    }

    public record LeadershipCandidateDto(string Username, int Score, string? Notes);

    [HttpPost("leadership-candidates")]
    [Authorize(nameof(AppRole.HrAdmin))]
    public async Task<IActionResult> AddLeadershipCandidate(LeadershipCandidateDto dto)
    {
        var userId = await ResolveUserId(dto.Username);
        if (userId is null) return NotFound(new { error = "Employee not found." });
        if (await db.LeadershipCandidates.AnyAsync(c => c.CandidateId == userId))
            return Conflict(new { error = "Candidate already listed." });
        var lc = new LeadershipCandidate { CandidateId = userId, Score = dto.Score, Notes = dto.Notes, SelectedById = UserId };
        db.LeadershipCandidates.Add(lc);
        await db.SaveChangesAsync();
        return Ok(lc);
    }

    private async Task<List<string>?> ResolveUserIds(IEnumerable<string> usernames)
    {
        var ids = new List<string>();
        foreach (var u in usernames)
        {
            var id = await ResolveUserId(u);
            if (id is null) return null;
            ids.Add(id);
        }
        return ids;
    }

    private Task<string?> ResolveUserId(string username) =>
        db.Employees.Where(e => e.UserName == username).Select(e => e.Id).FirstOrDefaultAsync();
}
