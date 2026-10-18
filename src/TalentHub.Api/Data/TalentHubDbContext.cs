using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TalentHub.Api.Domain;

namespace TalentHub.Api.Data;

public class TalentHubDbContext(DbContextOptions<TalentHubDbContext> options)
    : IdentityDbContext<Employee>(options)
{
    public DbSet<JobFamily> JobFamilies => Set<JobFamily>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<AlternativeCareerPath> AlternativeCareerPaths => Set<AlternativeCareerPath>();
    public DbSet<Scale> Scales => Set<Scale>();
    public DbSet<ScaleLevel> ScaleLevels => Set<ScaleLevel>();
    public DbSet<CareerPreference> CareerPreferences => Set<CareerPreference>();
    public DbSet<CareerPreferenceJob> CareerPreferenceJobs => Set<CareerPreferenceJob>();
    public DbSet<TalentReviewCycle> TalentReviewCycles => Set<TalentReviewCycle>();
    public DbSet<CycleCompetency> CycleCompetencies => Set<CycleCompetency>();
    public DbSet<TalentAssessment> TalentAssessments => Set<TalentAssessment>();
    public DbSet<AssessmentCompetencyScore> AssessmentCompetencyScores => Set<AssessmentCompetencyScore>();
    public DbSet<NineBoxPlacement> NineBoxPlacements => Set<NineBoxPlacement>();
    public DbSet<CriticalPosition> CriticalPositions => Set<CriticalPosition>();
    public DbSet<SuccessionPlan> SuccessionPlans => Set<SuccessionPlan>();
    public DbSet<DevelopmentPlanConfig> DevelopmentPlanConfigs => Set<DevelopmentPlanConfig>();
    public DbSet<DevelopmentPlan> DevelopmentPlans => Set<DevelopmentPlan>();
    public DbSet<DevelopmentPlanItem> DevelopmentPlanItems => Set<DevelopmentPlanItem>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<ReviewBoard> ReviewBoards => Set<ReviewBoard>();
    public DbSet<ReviewBoardMember> ReviewBoardMembers => Set<ReviewBoardMember>();
    public DbSet<ReviewMeeting> ReviewMeetings => Set<ReviewMeeting>();
    public DbSet<ReviewMeetingMember> ReviewMeetingMembers => Set<ReviewMeetingMember>();
    public DbSet<LeadershipCandidate> LeadershipCandidates => Set<LeadershipCandidate>();

    public new IQueryable<Employee> Employees => Users;
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<JobFamily>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.HasMany(x => x.Jobs).WithOne(x => x.JobFamily).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Job>(e => e.HasIndex(x => new { x.JobFamilyId, x.Title }).IsUnique());
        b.Entity<AlternativeCareerPath>(e =>
        {
            e.HasIndex(x => new { x.FromJobFamilyId, x.ToJobFamilyId }).IsUnique();
            e.HasOne(x => x.FromJobFamily).WithMany(x => x.PathsFrom).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ToJobFamily).WithMany(x => x.PathsTo).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Scale>(e => e.HasIndex(x => x.Name).IsUnique());
        b.Entity<ScaleLevel>(e => e.HasIndex(x => new { x.ScaleId, x.Value }).IsUnique());
        b.Entity<CareerPreference>(e =>
        {
            e.HasIndex(x => x.EmployeeId).IsUnique();
            e.HasOne(x => x.Employee).WithOne(x => x.CareerPreference)
                .HasForeignKey<CareerPreference>(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<CareerPreferenceJob>(e =>
            e.HasIndex(x => new { x.CareerPreferenceId, x.JobId }).IsUnique());
        b.Entity<CycleCompetency>(e =>
            e.HasIndex(x => new { x.TalentReviewCycleId, x.Name }).IsUnique());
        b.Entity<Employee>(e =>
        {
            e.HasOne(x => x.Supervisor).WithMany().HasForeignKey(x => x.SupervisorRefId).OnDelete(DeleteBehavior.Restrict);
            e.Ignore(x => x.SupervisorId);
        });
        b.Entity<TalentAssessment>(e =>
        {
            e.HasIndex(x => new { x.CycleId, x.SubjectId, x.ReviewerId }).IsUnique();
            e.HasOne(x => x.Subject).WithMany(x => x.AssessmentsAsSubject).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reviewer).WithMany(x => x.AssessmentsAsReviewer).HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AssessmentCompetencyScore>(e =>
            e.HasIndex(x => new { x.TalentAssessmentId, x.CycleCompetencyId }).IsUnique());
        b.Entity<NineBoxPlacement>(e => e.HasIndex(x => x.TalentAssessmentId).IsUnique());
        b.Entity<SuccessionPlan>(e =>
            e.HasIndex(x => new { x.CriticalPositionId, x.SuccessorEmployeeId }).IsUnique());
        b.Entity<DevelopmentPlanConfig>().HasData(new DevelopmentPlanConfig
        {
            Id = 1,
            ReviewFrequency = ReviewFrequency.Yearly,
            ReviewPeriodMonths = 12,
            OverallRatingMethod = OverallRatingMethod.AverageOfCompetencies
        });
        b.Entity<DevelopmentPlanItem>(e =>
            e.HasOne(x => x.DevelopmentPlan).WithMany(x => x.Items).OnDelete(DeleteBehavior.Cascade));
        b.Entity<ReviewBoard>(e => e.HasIndex(x => x.Name).IsUnique());
        b.Entity<ReviewBoardMember>(e =>
        {
            e.HasIndex(x => new { x.ReviewBoardId, x.MemberEmployeeId }).IsUnique();
            e.HasOne(x => x.Member).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ReviewBoard>(e => e.HasOne(x => x.HeadEmployee).WithMany().OnDelete(DeleteBehavior.Restrict));
        b.Entity<ReviewMeetingMember>(e =>
        {
            e.HasIndex(x => new { x.ReviewMeetingId, x.MemberEmployeeId }).IsUnique();
            e.HasOne(x => x.Member).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ReviewMeeting>(e => e.HasOne(x => x.HeadEmployee).WithMany().OnDelete(DeleteBehavior.Restrict));
        b.Entity<LeadershipCandidate>(e =>
        {
            e.HasIndex(x => x.CandidateId).IsUnique();
            e.HasOne(x => x.Candidate).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SelectedBy).WithMany().OnDelete(DeleteBehavior.Restrict);
        });
    }
}
