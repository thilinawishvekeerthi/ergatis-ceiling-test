using System.Text.Json;

namespace Talent.Assessment.NineBox;

/// <summary>One box as configured: its label, colour and reporting band.</summary>
internal sealed record NineBoxCell(int BoxNumber, int Performance, int Potential, string Label, string Colour, string Band);

/// <summary>
/// The nine boxes of a published grid config version (ADR-03), read from its payload.
/// Columns are performance (low to high), rows are potential (low to high), both zero-based in the payload.
/// </summary>
internal sealed class NineBoxScheme
{
    public const int Levels = 3;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly NineBoxCell[] _cells;

    private NineBoxScheme(NineBoxCell[] cells, Guid? performanceScaleVersionId, Guid? potentialScaleVersionId)
    {
        _cells = cells;
        PerformanceScaleVersionId = performanceScaleVersionId;
        PotentialScaleVersionId = potentialScaleVersionId;
    }

    /// <summary>The scale bound to the columns. Its levels carry the performance rubric.</summary>
    public Guid? PerformanceScaleVersionId { get; }

    /// <summary>The scale bound to the rows. Its levels carry the potential rubric.</summary>
    public Guid? PotentialScaleVersionId { get; }

    /// <summary>Ordered by box number 1 to 9.</summary>
    public IReadOnlyList<NineBoxCell> Cells => _cells;

    public NineBoxCell CellAt(int performance, int potential) => _cells[NineBoxRules.BoxNumber(performance, potential) - 1];

    public static NineBoxScheme Parse(string gridPayloadJson)
    {
        var grid = JsonSerializer.Deserialize<GridDto>(gridPayloadJson, Json)
                   ?? throw new InvalidOperationException("The nine-box grid payload is empty.");

        if (grid.ColumnBounds?.Count != Levels || grid.RowBounds?.Count != Levels || grid.Cells?.Count != Levels * Levels)
        {
            throw new InvalidOperationException("The nine-box evaluation needs a 3 x 3 grid.");
        }

        var cells = new NineBoxCell?[Levels * Levels];
        foreach (var c in grid.Cells)
        {
            if (c.Column is < 0 or >= Levels || c.Row is < 0 or >= Levels)
            {
                throw new InvalidOperationException($"Cell ({c.Column},{c.Row}) is outside the 3 x 3 grid.");
            }

            var box = NineBoxRules.BoxNumber(c.Column + 1, c.Row + 1);
            cells[box - 1] = new NineBoxCell(box, c.Column + 1, c.Row + 1, c.Label ?? "", c.Colour ?? "", c.Band ?? "");
        }

        if (cells.Any(c => c is null))
        {
            throw new InvalidOperationException("The nine-box grid defines the same cell twice.");
        }

        return new NineBoxScheme(cells!, grid.ColumnScaleVersionId, grid.RowScaleVersionId);
    }

    private sealed record GridDto(
        Guid? ColumnScaleVersionId, Guid? RowScaleVersionId, List<decimal>? ColumnBounds, List<decimal>? RowBounds, List<CellDto>? Cells);

    private sealed record CellDto(int Column, int Row, string? Label, string? Colour, string? Band);
}

/// <summary>One level of a rating scale with its one-line rubric, read from a scale config payload.</summary>
internal sealed record RubricLevel(int Level, string Label, string? Description);

internal static class RubricScale
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<RubricLevel> Parse(string scalePayloadJson)
    {
        var scale = JsonSerializer.Deserialize<ScaleDto>(scalePayloadJson, Json);
        return scale?.Levels?.Select(l => new RubricLevel(l.Level, l.Label ?? "", l.Description)).OrderBy(l => l.Level).ToList()
               ?? throw new InvalidOperationException("The scale payload has no levels.");
    }

    private sealed record ScaleDto(List<LevelDto>? Levels);

    private sealed record LevelDto(int Level, string? Label, string? Description);
}
