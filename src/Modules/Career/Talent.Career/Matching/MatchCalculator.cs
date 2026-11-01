using Talent.Career.Jobs;

namespace Talent.Career.Matching;

/// <summary>How one requirement stands for one employee. <see cref="Gap"/> is 0 when the requirement is met.</summary>
internal sealed record RequirementStatus(string CompetencyCode, int RequiredLevel, int CurrentLevel, int Weight)
{
    public int Gap => Math.Max(0, RequiredLevel - CurrentLevel);

    public bool IsMet => Gap == 0;
}

internal sealed record MatchResult(decimal Percent, IReadOnlyList<RequirementStatus> Requirements)
{
    public IEnumerable<RequirementStatus> Gaps => Requirements.Where(r => !r.IsMet);
}

/// <summary>
/// % match between an employee and a job. Each requirement scores current / required, capped at 1, so being above the level
/// earns no extra. The scores are averaged by weight. A job with no requirements matches 100%.
/// </summary>
internal static class MatchCalculator
{
    public static MatchResult Calculate(IReadOnlyList<JobRequirement> requirements, IReadOnlyDictionary<string, int> employeeLevels)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(employeeLevels);

        var statuses = requirements
            .OrderBy(r => r.CompetencyCode, StringComparer.Ordinal)
            .Select(r => new RequirementStatus(r.CompetencyCode, r.RequiredLevel, employeeLevels.GetValueOrDefault(r.CompetencyCode), r.Weight))
            .ToList();

        var totalWeight = statuses.Sum(s => s.Weight);
        if (totalWeight == 0)
        {
            return new MatchResult(100m, statuses);
        }

        var earned = statuses.Sum(s => s.Weight * Math.Min(1m, (decimal)s.CurrentLevel / s.RequiredLevel));
        return new MatchResult(Math.Round(earned / totalWeight * 100m, 1, MidpointRounding.AwayFromZero), statuses);
    }
}
