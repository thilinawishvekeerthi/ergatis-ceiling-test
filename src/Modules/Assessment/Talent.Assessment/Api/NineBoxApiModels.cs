using Talent.Assessment.NineBox;
using Talent.Security.Contracts;

namespace Talent.Assessment.Api;

/// <summary>POST body. Nullable so a missing field is a clear 400, not a silent 0.</summary>
internal sealed record EvaluateRequest(Guid? EmployeeId, int? Performance, int? Potential);

internal sealed record RubricLevelResponse(int Level, string Label, string? Description);

/// <summary>One box. Column and row are zero-based (performance and potential minus 1), as in the grid config.</summary>
internal sealed record NineBoxCellResponse(int BoxNumber, int Column, int Row, string Label, string Colour, string Band);

internal sealed record NineBoxGridResponse(
    Guid GridVersionId,
    IReadOnlyList<RubricLevelResponse> PerformanceLevels,
    IReadOnlyList<RubricLevelResponse> PotentialLevels,
    IReadOnlyList<decimal> ColumnBounds,
    IReadOnlyList<decimal> RowBounds,
    IReadOnlyList<NineBoxCellResponse> Cells)
{
    public static NineBoxGridResponse From(NineBoxConfig config) => new(
        config.GridVersionId,
        [.. config.PerformanceLevels.Select(l => new RubricLevelResponse(l.Level, l.Label, l.Description))],
        [.. config.PotentialLevels.Select(l => new RubricLevelResponse(l.Level, l.Label, l.Description))],
        NineBoxDefaults.Bounds,
        NineBoxDefaults.Bounds,
        [.. config.Scheme.Cells.Select(c => new NineBoxCellResponse(c.BoxNumber, c.Performance - 1, c.Potential - 1, c.Label, c.Colour, c.Band))]);
}

internal sealed record EvaluationResponse(
    Guid EvaluationId,
    Guid EmployeeId,
    int Performance,
    int Potential,
    int BoxNumber,
    string Label,
    int TalentScore,
    string Band,
    Guid GridVersionId,
    string EvaluatedBy,
    DateTimeOffset EvaluatedAt)
{
    public static EvaluationResponse From(NineBoxEvaluation e) => new(
        e.Id, e.EmployeeId, e.PerformanceLevel, e.PotentialLevel, e.BoxNumber, e.BoxLabel, e.TalentScore, e.Band, e.GridVersionId, e.ReviewerId, e.EvaluatedAt);
}

/// <summary>A row of the employee list. The latest placement is talent-confidential and is masked without an explicit grant.</summary>
internal sealed record EmployeePlacementRow(
    [property: FieldClassification(FieldClass.Public)] Guid EmployeeId,
    [property: FieldClassification(FieldClass.Public)] string EmployeeNumber,
    [property: FieldClassification(FieldClass.Public)] string Name,
    [property: FieldClassification(FieldClass.Internal)] string? Position,
    [property: FieldClassification(FieldClass.Internal)] string? Department,
    [property: FieldClassification(FieldClass.TalentConfidential)] EvaluationResponse? Latest);
