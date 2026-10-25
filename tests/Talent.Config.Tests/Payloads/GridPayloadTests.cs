using Talent.Config.Contracts;
using Talent.Config.Grids;
using Talent.Config.Scales;

namespace Talent.Config.Tests.Payloads;

public sealed class GridPayloadTests
{
    private const string TwoByTwo = """
        {"mode":"Calculated","columnBounds":[1,3],"rowBounds":[1,3],
         "cells":[{"column":0,"row":0,"label":"A","colour":"#aaaaaa","band":"LOW"},
                  {"column":1,"row":0,"label":"B","colour":"#bbbbbb","band":"MEDIUM"},
                  {"column":0,"row":1,"label":"C","colour":"#cccccc","band":"MEDIUM"},
                  {"column":1,"row":1,"label":"D","colour":"#dddddd","band":"HIGH"}]}
        """;

    [Fact]
    public void Parse_WhenValid_BuildsGridAndMode()
    {
        var payload = GridPayload.Parse(TwoByTwo);

        payload.Mode.ShouldBe(PlacementMode.Calculated);
        payload.Grid.Columns.ShouldBe(2);
        payload.Grid.Place(3m, 1m).ShouldBe(new GridCell(1, 0));
        payload.Grid.CellAt(new GridCell(1, 1)).ReportingBand.ShouldBe("HIGH");
    }

    [Fact]
    public void Parse_WhenGridTooSmall_ThrowsInvalidPayload() =>
        Should.Throw<InvalidConfigPayloadException>(() => GridPayload.Parse(
            """{"mode":"Manual","columnBounds":[1],"rowBounds":[1,2],"cells":[]}"""));

    [Fact]
    public void Parse_WhenModeUnknown_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => GridPayload.Parse(TwoByTwo.Replace("Calculated", "Magic", StringComparison.Ordinal)));

    [Fact]
    public void Parse_WhenColourNotHex_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => GridPayload.Parse(TwoByTwo.Replace("#aaaaaa", "red", StringComparison.Ordinal)));

    [Fact]
    public void Parse_WhenBandBlank_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => GridPayload.Parse(TwoByTwo.Replace("\"band\":\"LOW\"", "\"band\":\"\"", StringComparison.Ordinal)));

    [Fact]
    public void Parse_WhenNotJson_Throws() =>
        Should.Throw<InvalidConfigPayloadException>(() => GridPayload.Parse("nope"));

    [Fact]
    public void Parse_WhenCellMissing_ThrowsInvalidPayload() =>
        Should.Throw<InvalidConfigPayloadException>(() => GridPayload.Parse(
            """{"mode":"Manual","columnBounds":[1,2],"rowBounds":[1,2],"cells":[{"column":0,"row":0,"label":"A","colour":"#aaaaaa","band":"X"}]}"""));
}
