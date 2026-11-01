using System.Text.RegularExpressions;

namespace Talent.Career.Jobs;

/// <summary>One competency a job asks for, at a level from 1 to <see cref="Job.MaxCompetencyLevel"/>. A higher weight counts more in the % match.</summary>
internal sealed class JobRequirement
{
    // For EF Core.
    private JobRequirement() => CompetencyCode = "";

    public JobRequirement(string competencyCode, int requiredLevel, int weight = 1)
    {
        CompetencyCode = competencyCode;
        RequiredLevel = requiredLevel;
        Weight = weight;
    }

    public Guid JobId { get; private set; }
    public string CompetencyCode { get; private set; }
    public int RequiredLevel { get; private set; }
    public int Weight { get; private set; }

    internal void Set(int requiredLevel, int weight)
    {
        RequiredLevel = requiredLevel;
        Weight = weight;
    }
}

/// <summary>A role in the job catalogue with the competencies it requires. Careers move between jobs along <c>CareerPath</c> edges.</summary>
internal sealed partial class Job
{
    public const int MinCompetencyLevel = 1;
    public const int MaxCompetencyLevel = 5;
    public const int MaxWeight = 10;
    public const int MaxRequirements = 30;

    private readonly List<JobRequirement> _requirements = [];

    // For EF Core.
    private Job()
    {
        Code = "";
        Title = "";
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Title { get; private set; }

    /// <summary>Seniority grade. A higher number is a more senior job.</summary>
    public int Level { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyList<JobRequirement> Requirements => _requirements;

    public static Job Create(string code, string title, int level, string? description, IEnumerable<JobRequirement> requirements)
    {
        if (string.IsNullOrEmpty(code) || !CodePattern().IsMatch(code))
        {
            throw new InvalidCareerException("The job code must be 2 to 50 characters: capital letters, digits and underscores, starting with a letter.");
        }

        var job = new Job { Id = Guid.NewGuid(), Code = code, IsActive = true };
        job.Apply(title, level, description, requirements);
        return job;
    }

    /// <summary>Replaces the details and the requirements. Existing requirement rows are updated in place so EF Core sees a small change.</summary>
    public void Update(string title, int level, string? description, bool isActive, IEnumerable<JobRequirement> requirements)
    {
        Apply(title, level, description, requirements);
        IsActive = isActive;
    }

    private void Apply(string title, int level, string? description, IEnumerable<JobRequirement> requirements)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            throw new InvalidCareerException("The job title must be 1 to 200 characters.");
        }

        if (level is < 1 or > 20)
        {
            throw new InvalidCareerException("The job level must be from 1 to 20.");
        }

        if (description is { Length: > 2000 })
        {
            throw new InvalidCareerException("The description can be at most 2000 characters.");
        }

        var wanted = requirements.ToList();
        if (wanted.Count > MaxRequirements)
        {
            throw new InvalidCareerException($"A job can have at most {MaxRequirements} requirements.");
        }

        foreach (var r in wanted)
        {
            if (!CompetencyCodes.IsValid(r.CompetencyCode))
            {
                throw new InvalidCareerException($"'{r.CompetencyCode}' is not a valid competency code.");
            }

            if (r.RequiredLevel is < MinCompetencyLevel or > MaxCompetencyLevel)
            {
                throw new InvalidCareerException($"{r.CompetencyCode}: the required level must be {MinCompetencyLevel} to {MaxCompetencyLevel}.");
            }

            if (r.Weight is < 1 or > MaxWeight)
            {
                throw new InvalidCareerException($"{r.CompetencyCode}: the weight must be 1 to {MaxWeight}.");
            }
        }

        if (wanted.Select(r => r.CompetencyCode).Distinct(StringComparer.Ordinal).Count() != wanted.Count)
        {
            throw new InvalidCareerException("A competency can appear only once in a job.");
        }

        Title = title.Trim();
        Level = level;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        _requirements.RemoveAll(existing => wanted.All(w => w.CompetencyCode != existing.CompetencyCode));
        foreach (var w in wanted)
        {
            var existing = _requirements.Find(r => r.CompetencyCode == w.CompetencyCode);
            if (existing is null)
            {
                _requirements.Add(new JobRequirement(w.CompetencyCode, w.RequiredLevel, w.Weight));
            }
            else
            {
                existing.Set(w.RequiredLevel, w.Weight);
            }
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex CodePattern();
}

internal static partial class CompetencyCodes
{
    public static bool IsValid(string? code) => !string.IsNullOrEmpty(code) && Pattern().IsMatch(code);

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex Pattern();
}
