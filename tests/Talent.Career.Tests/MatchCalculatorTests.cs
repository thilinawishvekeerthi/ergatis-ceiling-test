using Talent.Career.Jobs;
using Talent.Career.Matching;

namespace Talent.Career.Tests;

public sealed class MatchCalculatorTests
{
    private static JobRequirement Req(string code, int level, int weight = 1) => new(code, level, weight);

    private static Dictionary<string, int> Levels(params (string Code, int Level)[] levels) =>
        levels.ToDictionary(l => l.Code, l => l.Level);

    [Fact]
    public void Calculate_WhenEveryRequirementMet_Is100AndHasNoGaps()
    {
        var result = MatchCalculator.Calculate([Req("SQL", 3), Req("LEADERSHIP", 2)], Levels(("SQL", 3), ("LEADERSHIP", 4)));

        result.Percent.ShouldBe(100m);
        result.Gaps.ShouldBeEmpty();
    }

    [Fact]
    public void Calculate_AboveRequiredLevel_EarnsNoExtra()
    {
        var result = MatchCalculator.Calculate([Req("SQL", 2), Req("LEADERSHIP", 4)], Levels(("SQL", 5), ("LEADERSHIP", 2)));

        // SQL 1.0, LEADERSHIP 0.5 -> 75%
        result.Percent.ShouldBe(75m);
    }

    [Fact]
    public void Calculate_MissingCompetency_CountsAsLevelZero()
    {
        var result = MatchCalculator.Calculate([Req("SQL", 3)], Levels());

        result.Percent.ShouldBe(0m);
        var gap = result.Gaps.Single();
        (gap.CompetencyCode, gap.RequiredLevel, gap.CurrentLevel, gap.Gap).ShouldBe(("SQL", 3, 0, 3));
    }

    [Fact]
    public void Calculate_UsesWeights()
    {
        // SQL (weight 3) fully met, LEADERSHIP (weight 1) not at all -> 3 / 4 = 75%
        var result = MatchCalculator.Calculate([Req("SQL", 2, 3), Req("LEADERSHIP", 2, 1)], Levels(("SQL", 2)));

        result.Percent.ShouldBe(75m);
    }

    [Fact]
    public void Calculate_RoundsToOneDecimal()
    {
        // 1/3 + 1/3 + 1/3 of three requirements each 1/3 met -> 33.3
        var result = MatchCalculator.Calculate([Req("A_ONE", 3), Req("B_TWO", 3), Req("C_THREE", 3)], Levels(("A_ONE", 1), ("B_TWO", 1), ("C_THREE", 1)));

        result.Percent.ShouldBe(33.3m);
    }

    [Fact]
    public void Calculate_WhenJobHasNoRequirements_Is100()
    {
        MatchCalculator.Calculate([], Levels(("SQL", 1))).Percent.ShouldBe(100m);
    }

    [Fact]
    public void Calculate_ListsRequirementsInCodeOrder_WithMetAndUnmet()
    {
        var result = MatchCalculator.Calculate([Req("ZEBRA", 2), Req("ALPHA", 3)], Levels(("ALPHA", 3), ("ZEBRA", 1)));

        result.Requirements.Select(r => r.CompetencyCode).ShouldBe(["ALPHA", "ZEBRA"]);
        result.Requirements.Select(r => r.IsMet).ShouldBe([true, false]);
        result.Gaps.Select(r => r.CompetencyCode).ShouldBe(["ZEBRA"]);
    }
}
