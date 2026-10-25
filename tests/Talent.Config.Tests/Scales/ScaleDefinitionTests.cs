using Talent.Config.Scales;

namespace Talent.Config.Tests.Scales;

public sealed class ScaleDefinitionTests
{
    private static string Json(int levels)
    {
        var items = Enumerable.Range(1, levels)
            .Select(i => $$"""{"level":{{i}},"label":"L{{i}}","minScore":{{i}},"band":"B{{(i <= levels / 2 ? "LOW" : "HIGH")}}"}""");
        return $$"""{"levels":[{{string.Join(',', items)}}]}""";
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public void Parse_WhenLevelCountWithinLimits_ReturnsLevels(int levels)
    {
        var scale = ScaleDefinition.Parse(Json(levels));

        scale.Levels.Count.ShouldBe(levels);
        scale.Levels[0].Level.ShouldBe(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public void Parse_WhenLevelCountOutsideLimits_Throws(int levels) =>
        Should.Throw<InvalidConfigPayloadException>(() => ScaleDefinition.Parse(Json(levels)));

    [Fact]
    public void Parse_WhenMinScoresNotAscending_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => ScaleDefinition.Parse(
            """{"levels":[{"level":1,"label":"A","minScore":2,"band":"LOW"},{"level":2,"label":"B","minScore":2,"band":"HIGH"}]}"""));

    [Fact]
    public void Parse_WhenLevelNumbersSkip_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => ScaleDefinition.Parse(
            """{"levels":[{"level":1,"label":"A","minScore":1,"band":"LOW"},{"level":3,"label":"B","minScore":2,"band":"HIGH"}]}"""));

    [Fact]
    public void Parse_WhenLabelBlank_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => ScaleDefinition.Parse(
            """{"levels":[{"level":1,"label":" ","minScore":1,"band":"LOW"},{"level":2,"label":"B","minScore":2,"band":"HIGH"}]}"""));

    [Fact]
    public void Parse_WhenBandMissing_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => ScaleDefinition.Parse(
            """{"levels":[{"level":1,"label":"A","minScore":1},{"level":2,"label":"B","minScore":2,"band":"HIGH"}]}"""));

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"levels":[],"extra":1}""")]
    public void Parse_WhenJsonInvalid_Throws(string json) =>
        Should.Throw<InvalidConfigPayloadException>(() => ScaleDefinition.Parse(json));
}
