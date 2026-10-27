using Talent.Config.Grids;
using Talent.Config.Contracts;

namespace Talent.Config.Tests.Grids;

public sealed class GridDefinitionTests
{
    // Lower bounds per level: score >= bound belongs to that level.
    private static readonly decimal[] ThreeLevels = [1m, 2m, 4m];

    private static GridCellDefinition[] Cells(int columns, int rows) =>
        [.. from r in Enumerable.Range(0, rows)
            from c in Enumerable.Range(0, columns)
            select new GridCellDefinition(new GridCell(c, r), $"C{c}R{r}", "#cccccc", $"band-{c + r}")];

    private static GridDefinition Grid(decimal[] x, decimal[] y) =>
        GridDefinition.Create(x, y, Cells(x.Length, y.Length));

    [Theory]
    [InlineData(1.0, 0)]
    [InlineData(1.99, 0)]
    [InlineData(2.0, 1)]
    [InlineData(3.99, 1)]
    [InlineData(4.0, 2)]
    [InlineData(5.0, 2)]
    public void Place_WhenScoreInRange_ReturnsMatchingLevelOnBothAxes(double score, int expectedLevel)
    {
        var grid = Grid(ThreeLevels, ThreeLevels);

        var cell = grid.Place((decimal)score, (decimal)score);

        cell.ShouldBe(new GridCell(expectedLevel, expectedLevel));
    }

    [Fact]
    public void Place_WhenAxesDiffer_UsesPerformanceAsColumnAndPotentialAsRow()
    {
        var grid = Grid([1m, 3m], [1m, 2m, 4m]);

        grid.Place(3m, 4m).ShouldBe(new GridCell(1, 2));
    }

    [Fact]
    public void Place_WhenScoreBelowFirstBound_Throws()
    {
        var grid = Grid(ThreeLevels, ThreeLevels);

        Should.Throw<ArgumentOutOfRangeException>(() => grid.Place(0.5m, 2m));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(2, 5)]
    public void Create_WhenSizeWithinLimits_Succeeds(int columns, int rows)
    {
        var grid = Grid(Bounds(columns), Bounds(rows));

        grid.Columns.ShouldBe(columns);
        grid.Rows.ShouldBe(rows);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    [InlineData(6, 3)]
    [InlineData(3, 6)]
    public void Create_WhenSizeOutsideLimits_Throws(int columns, int rows)
    {
        Should.Throw<InvalidGridException>(() =>
            GridDefinition.Create(Bounds(columns), Bounds(rows), Cells(columns, rows)));
    }

    [Fact]
    public void Create_WhenBoundsNotAscending_Throws()
    {
        Should.Throw<InvalidGridException>(() =>
            GridDefinition.Create([1m, 3m, 2m], ThreeLevels, Cells(3, 3)));
    }

    [Fact]
    public void Create_WhenCellMissing_Throws()
    {
        Should.Throw<InvalidGridException>(() =>
            GridDefinition.Create(ThreeLevels, ThreeLevels, Cells(3, 3)[..8]));
    }

    [Fact]
    public void Create_WhenCellDuplicated_Throws()
    {
        var cells = Cells(3, 3);
        cells[8] = cells[0];

        Should.Throw<InvalidGridException>(() => GridDefinition.Create(ThreeLevels, ThreeLevels, cells));
    }

    [Fact]
    public void Create_WhenCellOutsideGrid_Throws()
    {
        var cells = Cells(2, 2);
        cells[3] = cells[3] with { Cell = new GridCell(2, 2) };

        Should.Throw<InvalidGridException>(() => GridDefinition.Create([1m, 2m], [1m, 2m], cells));
    }

    [Fact]
    public void Create_WhenCellLabelBlank_Throws()
    {
        var cells = Cells(2, 2);
        cells[0] = cells[0] with { Label = " " };

        Should.Throw<InvalidGridException>(() => GridDefinition.Create([1m, 2m], [1m, 2m], cells));
    }

    [Fact]
    public void CellAt_WhenCellExists_ReturnsItsDefinition()
    {
        var grid = Grid(ThreeLevels, ThreeLevels);

        grid.CellAt(new GridCell(1, 2)).Label.ShouldBe("C1R2");
    }

    private static decimal[] Bounds(int levels) =>
        [.. Enumerable.Range(1, levels).Select(i => (decimal)i)];
}
