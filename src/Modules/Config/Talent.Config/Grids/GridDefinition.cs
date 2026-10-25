using Talent.Config.Contracts;

namespace Talent.Config.Grids;

/// <summary>
/// A configured N x M grid (ADR-03), for example the nine-box.
/// Each axis is a list of ascending lower bounds: a score belongs to the last level whose bound it reaches.
/// </summary>
internal sealed class GridDefinition
{
    public const int MinLevels = 2;
    public const int MaxLevels = 5;

    private readonly decimal[] _columnBounds;
    private readonly decimal[] _rowBounds;
    private readonly Dictionary<GridCell, GridCellDefinition> _cells;

    private GridDefinition(decimal[] columnBounds, decimal[] rowBounds, Dictionary<GridCell, GridCellDefinition> cells)
    {
        _columnBounds = columnBounds;
        _rowBounds = rowBounds;
        _cells = cells;
    }

    public int Columns => _columnBounds.Length;
    public int Rows => _rowBounds.Length;
    public IReadOnlyCollection<GridCellDefinition> Cells => _cells.Values;

    public static GridDefinition Create(
        IReadOnlyList<decimal> columnBounds,
        IReadOnlyList<decimal> rowBounds,
        IReadOnlyList<GridCellDefinition> cells)
    {
        ValidateAxis(columnBounds, "column");
        ValidateAxis(rowBounds, "row");

        var map = new Dictionary<GridCell, GridCellDefinition>();
        foreach (var definition in cells)
        {
            var cell = definition.Cell;
            if (cell.Column < 0 || cell.Column >= columnBounds.Count || cell.Row < 0 || cell.Row >= rowBounds.Count)
            {
                throw new InvalidGridException($"Cell ({cell.Column},{cell.Row}) is outside the grid.");
            }

            if (string.IsNullOrWhiteSpace(definition.Label))
            {
                throw new InvalidGridException($"Cell ({cell.Column},{cell.Row}) needs a label.");
            }

            if (!map.TryAdd(cell, definition))
            {
                throw new InvalidGridException($"Cell ({cell.Column},{cell.Row}) is defined more than once.");
            }
        }

        var expected = columnBounds.Count * rowBounds.Count;
        if (map.Count != expected)
        {
            throw new InvalidGridException($"Expected {expected} cells but found {map.Count}.");
        }

        return new GridDefinition([.. columnBounds], [.. rowBounds], map);
    }

    /// <summary>Maps a score on each axis to its cell. Column uses <paramref name="columnScore"/>, row uses <paramref name="rowScore"/>.</summary>
    public GridCell Place(decimal columnScore, decimal rowScore) =>
        new(LevelOf(_columnBounds, columnScore, nameof(columnScore)), LevelOf(_rowBounds, rowScore, nameof(rowScore)));

    public GridCellDefinition CellAt(GridCell cell) =>
        _cells.TryGetValue(cell, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(cell), cell, "No such cell in this grid.");

    private static int LevelOf(decimal[] bounds, decimal score, string paramName)
    {
        if (score < bounds[0])
        {
            throw new ArgumentOutOfRangeException(paramName, score, $"Score is below the lowest bound {bounds[0]}.");
        }

        var level = 0;
        for (var i = 1; i < bounds.Length && score >= bounds[i]; i++)
        {
            level = i;
        }

        return level;
    }

    private static void ValidateAxis(IReadOnlyList<decimal> bounds, string axis)
    {
        if (bounds.Count is < MinLevels or > MaxLevels)
        {
            throw new InvalidGridException($"The {axis} axis needs {MinLevels} to {MaxLevels} levels but has {bounds.Count}.");
        }

        for (var i = 1; i < bounds.Count; i++)
        {
            if (bounds[i] <= bounds[i - 1])
            {
                throw new InvalidGridException($"The {axis} bounds must be strictly ascending.");
            }
        }
    }
}
