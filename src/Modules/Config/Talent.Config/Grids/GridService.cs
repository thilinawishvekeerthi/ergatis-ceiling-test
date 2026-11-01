using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Config.Versions;

namespace Talent.Config.Grids;

internal sealed class GridService(ConfigDbContext db) : IGridService
{
    public async Task<GridLayoutDto?> GetLayoutAsync(Guid gridVersionId, CancellationToken cancellationToken)
    {
        var payload = await LoadAsync(gridVersionId, cancellationToken);
        return payload is null
            ? null
            : new GridLayoutDto(
                gridVersionId,
                payload.Mode,
                payload.Grid.Columns,
                payload.Grid.Rows,
                payload.ColumnScaleVersionId,
                payload.RowScaleVersionId,
                [.. payload.Grid.Cells
                    .OrderBy(c => c.Cell.Row).ThenBy(c => c.Cell.Column)
                    .Select(c => new GridCellDto(c.Cell.Column, c.Cell.Row, c.Label, c.Colour, c.ReportingBand))]);
    }

    public async Task<GridCell> PlaceAsync(Guid gridVersionId, decimal columnScore, decimal rowScore, CancellationToken cancellationToken)
    {
        var payload = await LoadAsync(gridVersionId, cancellationToken)
                      ?? throw new ConfigNotFoundException("Grid version", gridVersionId);
        return payload.Grid.Place(columnScore, rowScore);
    }

    private async Task<GridPayload?> LoadAsync(Guid gridVersionId, CancellationToken cancellationToken)
    {
        var json = await (from v in db.Versions
                          join d in db.Definitions on v.DefinitionId equals d.Id
                          where v.Id == gridVersionId && d.Type == ConfigType.Grid
                          select v.PayloadJson)
            .SingleOrDefaultAsync(cancellationToken);
        return json is null ? null : GridPayload.Parse(json);
    }
}
