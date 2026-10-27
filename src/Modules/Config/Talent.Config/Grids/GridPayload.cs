using System.Text.RegularExpressions;
using Talent.Config.Contracts;

namespace Talent.Config.Grids;

/// <summary>A grid config payload: the grid, its placement mode and the scales its axes are bound to (ADR-03).</summary>
internal sealed partial class GridPayload
{
    private GridPayload(
        GridDefinition grid, PlacementMode mode, Guid? columnScaleVersionId, Guid? rowScaleVersionId)
    {
        Grid = grid;
        Mode = mode;
        ColumnScaleVersionId = columnScaleVersionId;
        RowScaleVersionId = rowScaleVersionId;
    }

    public GridDefinition Grid { get; }
    public PlacementMode Mode { get; }
    public Guid? ColumnScaleVersionId { get; }
    public Guid? RowScaleVersionId { get; }

    public static GridPayload Parse(string json)
    {
        var dto = PayloadJson.Read<Dto>(json);

        if (dto.ColumnBounds is null || dto.RowBounds is null || dto.Cells is null)
        {
            throw new InvalidConfigPayloadException("A grid needs 'columnBounds', 'rowBounds' and 'cells'.");
        }

        var cells = new List<GridCellDefinition>(dto.Cells.Count);
        foreach (var cell in dto.Cells)
        {
            if (string.IsNullOrWhiteSpace(cell.Band))
            {
                throw new InvalidConfigPayloadException($"Cell ({cell.Column},{cell.Row}) needs a reporting band.");
            }

            if (cell.Colour is null || !HexColour().IsMatch(cell.Colour))
            {
                throw new InvalidConfigPayloadException($"Cell ({cell.Column},{cell.Row}) needs a colour like #1a2b3c.");
            }

            cells.Add(new GridCellDefinition(new GridCell(cell.Column, cell.Row), cell.Label ?? "", cell.Colour, cell.Band.Trim()));
        }

        try
        {
            var grid = GridDefinition.Create(dto.ColumnBounds, dto.RowBounds, cells);
            return new GridPayload(grid, dto.Mode, dto.ColumnScaleVersionId, dto.RowScaleVersionId);
        }
        catch (InvalidGridException ex)
        {
            throw new InvalidConfigPayloadException(ex.Message);
        }
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColour();

    private sealed class Dto
    {
        public PlacementMode Mode { get; set; }
        public Guid? ColumnScaleVersionId { get; set; }
        public Guid? RowScaleVersionId { get; set; }
        public List<decimal>? ColumnBounds { get; set; }
        public List<decimal>? RowBounds { get; set; }
        public List<CellDto>? Cells { get; set; }
    }

    private sealed class CellDto
    {
        public int Column { get; set; }
        public int Row { get; set; }
        public string? Label { get; set; }
        public string? Colour { get; set; }
        public string? Band { get; set; }
    }
}
