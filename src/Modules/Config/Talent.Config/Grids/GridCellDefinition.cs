using Talent.Config.Contracts;

namespace Talent.Config.Grids;

/// <summary>Label, colour and stable reporting band of one grid cell (ADR-03).</summary>
internal sealed record GridCellDefinition(GridCell Cell, string Label, string Colour, string ReportingBand);
