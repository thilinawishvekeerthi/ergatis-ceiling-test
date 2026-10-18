using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace TalentHub.Api.Domain;

public class Employee : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Resume { get; set; }
    public int? JobId { get; set; }
    public Job? Job { get; set; }
    public int? SupervisorId { get; set; }
    [ForeignKey("Supervisor")] public string? SupervisorRefId { get; set; }
    public Employee? Supervisor { get; set; }
    public bool IsActive { get; set; } = true;
    public CareerPreference? CareerPreference { get; set; }
    public ICollection<TalentAssessment> AssessmentsAsSubject { get; set; } = new List<TalentAssessment>();
    public ICollection<TalentAssessment> AssessmentsAsReviewer { get; set; } = new List<TalentAssessment>();
}

public class CareerPreference
{
    public int Id { get; set; }
    [ForeignKey("Employee")]
    public string EmployeeId { get; set; } = string.Empty;
    public Employee Employee { get; set; } = null!;
    public bool WillingToRelocate { get; set; }
    public string? MobilityDetails { get; set; }
    public string? PreferredWorkstation { get; set; }
    public string? LongTermGoals { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CareerPreferenceJob> PreferredJobs { get; set; } = new List<CareerPreferenceJob>();
}

public class CareerPreferenceJob
{
    public int Id { get; set; }
    public int CareerPreferenceId { get; set; }
    public CareerPreference CareerPreference { get; set; } = null!;
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;
}
