using System.ComponentModel.DataAnnotations.Schema;

namespace TalentHub.Api.Domain;

public class DevelopmentPlanConfig
{
    public int Id { get; set; }
    public ReviewFrequency ReviewFrequency { get; set; }
    public int ReviewPeriodMonths { get; set; } = 12;
    public OverallRatingMethod OverallRatingMethod { get; set; } = OverallRatingMethod.AverageOfCompetencies;
}

public class DevelopmentPlan
{
    public int Id { get; set; }
    [ForeignKey("Employee")] public string EmployeeId { get; set; } = string.Empty;
    public Employee Employee { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DevelopmentPlanStatus Status { get; set; } = DevelopmentPlanStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? OverallRating { get; set; }
    public ICollection<DevelopmentPlanItem> Items { get; set; } = new List<DevelopmentPlanItem>();
}

public class DevelopmentPlanItem
{
    public int Id { get; set; }
    public int DevelopmentPlanId { get; set; }
    public DevelopmentPlan DevelopmentPlan { get; set; } = null!;
    public string CompetencyName { get; set; } = string.Empty;
    public string? Action { get; set; }
    public string? SupportRequired { get; set; }
    public string? MeasureOfSuccess { get; set; }
    public DateOnly? TargetDate { get; set; }
}
