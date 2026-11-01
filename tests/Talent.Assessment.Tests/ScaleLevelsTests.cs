using Talent.Assessment.Cycles;
using Talent.Assessment.Reviews;

namespace Talent.Assessment.Tests;

public sealed class ScaleLevelsTests
{
    private const string Json = """{"levels":[{"level":1,"label":"Low","minScore":1},{"level":2,"label":"Mid","minScore":2.5},{"level":3,"label":"High","minScore":4}]}""";

    [Fact]
    public void Parse_ReadsLevelsAndScores()
    {
        var scale = ScaleLevels.Parse(Json, "performance");

        scale.Levels.Order().ShouldBe([1, 2, 3]);
        scale.ScoreOf(2, "performance").ShouldBe(2.5m);
        scale.LabelOf(3).ShouldBe("High");
    }

    [Fact]
    public void ScoreOf_WhenLevelNotOnScale_Throws()
    {
        var scale = ScaleLevels.Parse(Json, "performance");

        Should.Throw<InvalidReviewException>(() => scale.ScoreOf(9, "performance")).Message.ShouldContain("1, 2, 3");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"levels":[]}""")]
    [InlineData("""{"levels":[{"level":1}]}""")]
    public void Parse_WhenPayloadBroken_Throws(string json) =>
        Should.Throw<InvalidReviewException>(() => ScaleLevels.Parse(json, "potential"));
}
