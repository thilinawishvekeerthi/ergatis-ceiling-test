namespace Talent.Config.Scales;

/// <summary>
/// Typed copy of a published scale level, so reports can query levels and bands (ADR-02).
/// Written when a scale version is published. The version payload stays the source of truth.
/// </summary>
internal sealed class ScaleLevelRow
{
    public Guid VersionId { get; set; }
    public int Level { get; set; }
    public string Label { get; set; } = "";
    public decimal MinScore { get; set; }
    public string Band { get; set; } = "";
}
