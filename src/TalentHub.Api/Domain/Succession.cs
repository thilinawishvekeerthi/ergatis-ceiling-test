using System.ComponentModel.DataAnnotations.Schema;

namespace TalentHub.Api.Domain;

public class CriticalPosition
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Department { get; set; }
    [ForeignKey("IncumbentEmployee")] public string? IncumbentEmployeeId { get; set; }
    public Employee? IncumbentEmployee { get; set; }
    public string? Notes { get; set; }
    public ICollection<SuccessionPlan> Plans { get; set; } = new List<SuccessionPlan>();
}

public class SuccessionPlan
{
    public int Id { get; set; }
    public int CriticalPositionId { get; set; }
    public CriticalPosition CriticalPosition { get; set; } = null!;
    [ForeignKey("SuccessorEmployee")] public string SuccessorEmployeeId { get; set; } = string.Empty;
    public Employee SuccessorEmployee { get; set; } = null!;
    public ReadinessLevel Readiness { get; set; } = ReadinessLevel.NotReady;
    public string? Suitability { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
