using System.ComponentModel.DataAnnotations.Schema;

namespace TalentHub.Api.Domain;

public class Alert
{
    public int Id { get; set; }
    [ForeignKey("Employee")] public string EmployeeId { get; set; } = string.Empty;
    public Employee? Employee { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
