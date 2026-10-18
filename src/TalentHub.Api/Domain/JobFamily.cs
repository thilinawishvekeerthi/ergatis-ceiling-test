namespace TalentHub.Api.Domain;

public class JobFamily
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Job> Jobs { get; set; } = new List<Job>();
    public ICollection<AlternativeCareerPath> PathsFrom { get; set; } = new List<AlternativeCareerPath>();
    public ICollection<AlternativeCareerPath> PathsTo { get; set; } = new List<AlternativeCareerPath>();
}

public class Job
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int JobFamilyId { get; set; }
    public JobFamily JobFamily { get; set; } = null!;
}

public class AlternativeCareerPath
{
    public int Id { get; set; }
    public int FromJobFamilyId { get; set; }
    public JobFamily FromJobFamily { get; set; } = null!;
    public int ToJobFamilyId { get; set; }
    public JobFamily ToJobFamily { get; set; } = null!;
    public string? Notes { get; set; }
}
