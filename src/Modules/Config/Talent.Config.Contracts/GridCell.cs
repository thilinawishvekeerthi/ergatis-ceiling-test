namespace Talent.Config.Contracts;

/// <summary>Zero-based position in a configured grid. Column = X axis, Row = Y axis.</summary>
public readonly record struct GridCell(int Column, int Row);
