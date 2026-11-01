using FluentValidation;
using Talent.Config.Contracts;
using Talent.Security.Contracts;

namespace Talent.Assessment.Api;

internal sealed record CycleResponse(
    [property: FieldClassification(FieldClass.Public)] Guid Id,
    [property: FieldClassification(FieldClass.Public)] string Name,
    [property: FieldClassification(FieldClass.Public)] DateOnly PeriodStart,
    [property: FieldClassification(FieldClass.Public)] DateOnly PeriodEnd,
    [property: FieldClassification(FieldClass.Public)] string Status,
    [property: FieldClassification(FieldClass.Public)] Guid PerformanceScaleVersionId,
    [property: FieldClassification(FieldClass.Public)] Guid PotentialScaleVersionId,
    [property: FieldClassification(FieldClass.Public)] Guid GridVersionId,
    [property: FieldClassification(FieldClass.Public)] DateTimeOffset? OpenedAt,
    [property: FieldClassification(FieldClass.Public)] DateTimeOffset? ClosedAt,
    [property: FieldClassification(FieldClass.Internal)] int? ReviewCount,
    [property: FieldClassification(FieldClass.Internal)] int? RatedCount);

/// <summary>
/// One review. Potential and the grid cell are talent-confidential: an employee never sees them on their own review,
/// and a role without the explicit grant never sees them on anyone's.
/// </summary>
internal sealed record ReviewResponse(
    [property: FieldClassification(FieldClass.Public)] Guid Id,
    [property: FieldClassification(FieldClass.Public)] Guid CycleId,
    [property: FieldClassification(FieldClass.Public)] Guid EmployeeId,
    [property: FieldClassification(FieldClass.Public)] string? EmployeeName,
    [property: FieldClassification(FieldClass.Public)] Guid ReviewerId,
    [property: FieldClassification(FieldClass.Public)] string Status,
    [property: FieldClassification(FieldClass.Internal)] int? PerformanceLevel,
    [property: FieldClassification(FieldClass.Internal)] string? PerformanceLabel,
    [property: FieldClassification(FieldClass.TalentConfidential)] int? PotentialLevel,
    [property: FieldClassification(FieldClass.Internal)] string? Comment,
    [property: FieldClassification(FieldClass.Public)] DateTimeOffset? RatedAt,
    [property: FieldClassification(FieldClass.TalentConfidential)] int? ProposedColumn,
    [property: FieldClassification(FieldClass.TalentConfidential)] int? ProposedRow,
    [property: FieldClassification(FieldClass.TalentConfidential)] int? FinalColumn,
    [property: FieldClassification(FieldClass.TalentConfidential)] int? FinalRow,
    [property: FieldClassification(FieldClass.TalentConfidential)] string? CalibratedBy,
    [property: FieldClassification(FieldClass.TalentConfidential)] string? CalibrationReason);

internal sealed record BoardPerson(Guid ReviewId, Guid EmployeeId, string? Name, int Column, int Row, bool Calibrated, int? ProposedColumn, int? ProposedRow);

/// <summary>The grid with the people on it. Only callers with the explicit talent-confidential grant get a board.</summary>
internal sealed record BoardResponse(
    Guid CycleId, string CycleName, string CycleStatus, string Mode, GridLayoutDto Layout, IReadOnlyList<BoardPerson> People);

internal sealed record CreateCycleRequest(
    string? Name, DateOnly? PeriodStart, DateOnly? PeriodEnd, string? PerformanceScaleCode, string? PotentialScaleCode, string? GridCode);

internal sealed record AssignReviewRequest(Guid? EmployeeId, Guid? ReviewerId);

internal sealed record RateRequest(int? PerformanceLevel, int? PotentialLevel, string? Comment, int? Column, int? Row);

internal sealed record CalibrateRequest(int? Column, int? Row, string? Reason);

internal sealed class CreateCycleRequestValidator : AbstractValidator<CreateCycleRequest>
{
    public CreateCycleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PeriodStart).NotNull().WithMessage("The start date is required.");
        RuleFor(x => x.PeriodEnd).NotNull().WithMessage("The end date is required.");
        RuleFor(x => x).Must(x => x.PeriodStart is null || x.PeriodEnd is null || x.PeriodEnd >= x.PeriodStart)
            .WithName("periodEnd").WithMessage("The cycle cannot end before it starts.");
    }
}

internal sealed class AssignReviewRequestValidator : AbstractValidator<AssignReviewRequest>
{
    public AssignReviewRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotNull().NotEqual(Guid.Empty);
        RuleFor(x => x.ReviewerId).NotNull().NotEqual(Guid.Empty);
    }
}

internal sealed class RateRequestValidator : AbstractValidator<RateRequest>
{
    public RateRequestValidator()
    {
        RuleFor(x => x.PerformanceLevel).NotNull().GreaterThanOrEqualTo(1);
        RuleFor(x => x.PotentialLevel).NotNull().GreaterThanOrEqualTo(1);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}

internal sealed class CalibrateRequestValidator : AbstractValidator<CalibrateRequest>
{
    public CalibrateRequestValidator()
    {
        RuleFor(x => x.Column).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.Row).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("A calibration change needs a reason.").MaximumLength(2000);
    }
}
