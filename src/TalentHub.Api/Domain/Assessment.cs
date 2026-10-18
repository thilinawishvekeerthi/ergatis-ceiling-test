using System.ComponentModel.DataAnnotations.Schema;

namespace TalentHub.Api.Domain;

public class TalentReviewCycle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsOpen { get; set; } = true;
    public int? ScaleId { get; set; }
    public Scale? Scale { get; set; }
    public ICollection<CycleCompetency> Competencies { get; set; } = new List<CycleCompetency>();
}

public class CycleCompetency
{
    public int Id { get; set; }
    public int TalentReviewCycleId { get; set; }
    public TalentReviewCycle TalentReviewCycle { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Weight { get; set; }
}

public class TalentAssessment
{
    public int Id { get; set; }
    public int CycleId { get; set; }
    public TalentReviewCycle Cycle { get; set; } = null!;
    [ForeignKey("Subject")] public string SubjectId { get; set; } = string.Empty;
    public Employee Subject { get; set; } = null!;
    [ForeignKey("Reviewer")] public string ReviewerId { get; set; } = string.Empty;
    public Employee Reviewer { get; set; } = null!;
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public int? PerformanceRating { get; set; }
    public int? PotentialRating { get; set; }
    public string? Remarks { get; set; }
    public ICollection<AssessmentCompetencyScore> Scores { get; set; } = new List<AssessmentCompetencyScore>();
    public NineBoxPlacement? Placement { get; set; }
}

public class AssessmentCompetencyScore
{
    public int Id { get; set; }
    public int TalentAssessmentId { get; set; }
    public TalentAssessment TalentAssessment { get; set; } = null!;
    public int CycleCompetencyId { get; set; }
    public CycleCompetency CycleCompetency { get; set; } = null!;
    public int Score { get; set; }
}

public class NineBoxPlacement
{
    public int Id { get; set; }
    public int TalentAssessmentId { get; set; }
    public TalentAssessment TalentAssessment { get; set; } = null!;
    public int PerformanceAxis { get; set; }
    public int PotentialAxis { get; set; }
    public string PlacedByRole { get; set; } = string.Empty;
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
