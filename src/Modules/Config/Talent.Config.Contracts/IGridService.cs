namespace Talent.Config.Contracts;

/// <summary>Label, colour and reporting band of one cell of a configured grid.</summary>
public sealed record GridCellDto(int Column, int Row, string Label, string Colour, string Band);

/// <summary>A grid as the board draws it. Column = X axis, Row = Y axis, both zero-based.</summary>
public sealed record GridLayoutDto(
    Guid VersionId,
    PlacementMode Mode,
    int Columns,
    int Rows,
    Guid? ColumnScaleVersionId,
    Guid? RowScaleVersionId,
    IReadOnlyList<GridCellDto> Cells);

/// <summary>Grid rules for other modules, so they never parse a grid payload themselves.</summary>
public interface IGridService
{
    /// <summary>Null when the version does not exist or is not a grid.</summary>
    Task<GridLayoutDto?> GetLayoutAsync(Guid gridVersionId, CancellationToken cancellationToken);

    /// <summary>Maps one score per axis to a cell. Throws <see cref="ArgumentOutOfRangeException"/> when a score is below the lowest bound.</summary>
    Task<GridCell> PlaceAsync(Guid gridVersionId, decimal columnScore, decimal rowScore, CancellationToken cancellationToken);
}
