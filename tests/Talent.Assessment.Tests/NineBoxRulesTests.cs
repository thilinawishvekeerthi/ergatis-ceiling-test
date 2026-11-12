using Talent.Assessment.NineBox;
using Talent.Shared.Errors;

namespace Talent.Assessment.Tests;

/// <summary>BRD §5 and acceptance criteria 1 to 3. The rules are a pure function, so no database is needed.</summary>
public sealed class NineBoxRulesTests
{
    private static readonly NineBoxScheme Scheme = NineBoxScheme.Parse(NineBoxDefaults.GridPayload(Guid.NewGuid(), Guid.NewGuid()));

    [Theory]
    [InlineData(1, 1, 1, "Risk", 1, "LOW")]
    [InlineData(2, 1, 2, "Effective", 2, "LOW")]
    [InlineData(3, 1, 3, "Trusted pro", 3, "WATCH")]
    [InlineData(1, 2, 4, "Inconsistent", 2, "LOW")]
    [InlineData(2, 2, 5, "Core player", 4, "WATCH")]
    [InlineData(3, 2, 6, "High performer", 6, "HIGH")]
    [InlineData(1, 3, 7, "Potential gem", 3, "WATCH")]
    [InlineData(2, 3, 8, "High potential", 6, "HIGH")]
    [InlineData(3, 3, 9, "Star", 9, "HIGH")]
    public void Place_AllNineCombinations_GiveBrdBoxLabelScoreAndBand(
        int performance, int potential, int box, string label, int score, string band)
    {
        var placement = NineBoxRules.Place(performance, potential, Scheme);

        placement.BoxNumber.ShouldBe(box);
        placement.Label.ShouldBe(label);
        placement.TalentScore.ShouldBe(score);
        placement.Band.ShouldBe(band);
        placement.Performance.ShouldBe(performance);
        placement.Potential.ShouldBe(potential);
    }

    [Fact]
    public void Place_EveryCombination_HasAUniqueBox()
    {
        var boxes = from p in Enumerable.Range(1, 3)
                    from q in Enumerable.Range(1, 3)
                    select NineBoxRules.Place(p, q, Scheme).BoxNumber;

        boxes.Order().ShouldBe(Enumerable.Range(1, 9));
    }

    [Theory]
    [InlineData(0, 2, "performance")]
    [InlineData(4, 2, "performance")]
    [InlineData(-1, 2, "performance")]
    [InlineData(2, 0, "potential")]
    [InlineData(2, 4, "potential")]
    public void Place_LevelOutside1To3_IsRejectedWithFieldError(int performance, int potential, string field)
    {
        var ex = Should.Throw<ValidationFailedException>(() => NineBoxRules.Place(performance, potential, Scheme));

        ex.Errors.Keys.ShouldBe([field]);
        ex.Errors[field].Single().ShouldContain("between 1 and 3");
    }

    [Fact]
    public void Place_BothLevelsOutOfRange_ReportsBothFields()
    {
        var ex = Should.Throw<ValidationFailedException>(() => NineBoxRules.Place(5, 0, Scheme));

        ex.Errors.Keys.Order().ShouldBe(["performance", "potential"]);
    }

    [Fact]
    public void DefaultGrid_BandsFollowTheScoreRule()
    {
        // BRD §5: High 6-9, Watch 3-4, Low 1-2. The default grid must agree for every cell.
        foreach (var p in Enumerable.Range(1, 3))
        {
            foreach (var q in Enumerable.Range(1, 3))
            {
                var placement = NineBoxRules.Place(p, q, Scheme);
                placement.Band.ShouldBe(NineBoxRules.BandForScore(placement.TalentScore));
            }
        }
    }

    [Fact]
    public void Parse_GridThatIsNotThreeByThree_IsRejected()
    {
        const string twoByTwo = """
            {"mode":"Calculated","columnBounds":[1,2],"rowBounds":[1,2],"cells":[
              {"column":0,"row":0,"label":"A","colour":"#ffffff","band":"LOW"},
              {"column":1,"row":0,"label":"B","colour":"#ffffff","band":"LOW"},
              {"column":0,"row":1,"label":"C","colour":"#ffffff","band":"LOW"},
              {"column":1,"row":1,"label":"D","colour":"#ffffff","band":"LOW"}]}
            """;

        Should.Throw<InvalidOperationException>(() => NineBoxScheme.Parse(twoByTwo)).Message.ShouldContain("3 x 3");
    }
}

public sealed class NineBoxEvaluationTests
{
    [Fact]
    public void Record_TrimsTimeToMicroseconds_SoTheReturnedTimeMatchesTheStoredTime()
    {
        var scheme = NineBoxScheme.Parse(NineBoxDefaults.GridPayload(Guid.NewGuid(), Guid.NewGuid()));
        var at = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

        var evaluation = NineBoxEvaluation.Record(Guid.NewGuid(), Guid.NewGuid(), NineBoxRules.Place(3, 3, scheme), "reviewer", at);

        evaluation.EvaluatedAt.UtcTicks.ShouldBe(at.UtcTicks - 7);
        evaluation.DomainEvents.Single().ShouldBeOfType<Talent.Assessment.Contracts.NineBoxEvaluationRecorded>().TalentScore.ShouldBe(9);
    }
}
