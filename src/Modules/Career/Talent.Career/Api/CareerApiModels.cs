using FluentValidation;
using Talent.Career.Jobs;
using Talent.Security.Contracts;

namespace Talent.Career.Api;

internal sealed record RequirementDto(string CompetencyCode, int RequiredLevel, int Weight);

internal sealed record JobResponse(
    [property: FieldClassification(FieldClass.Public)] Guid Id,
    [property: FieldClassification(FieldClass.Public)] string Code,
    [property: FieldClassification(FieldClass.Public)] string Title,
    [property: FieldClassification(FieldClass.Public)] int Level,
    [property: FieldClassification(FieldClass.Public)] string? Description,
    [property: FieldClassification(FieldClass.Public)] bool IsActive,
    [property: FieldClassification(FieldClass.Public)] IReadOnlyList<RequirementDto> Requirements)
{
    public static JobResponse From(Job job) => new(
        job.Id, job.Code, job.Title, job.Level, job.Description, job.IsActive,
        [.. job.Requirements.OrderBy(r => r.CompetencyCode, StringComparer.Ordinal).Select(r => new RequirementDto(r.CompetencyCode, r.RequiredLevel, r.Weight))]);
}

internal sealed record PathResponse(
    [property: FieldClassification(FieldClass.Public)] Guid Id,
    [property: FieldClassification(FieldClass.Public)] Guid FromJobId,
    [property: FieldClassification(FieldClass.Public)] string FromTitle,
    [property: FieldClassification(FieldClass.Public)] Guid ToJobId,
    [property: FieldClassification(FieldClass.Public)] string ToTitle);

internal sealed record JobSummary(Guid Id, string Code, string Title, int Level);

internal sealed record CompetencyLevelDto(string CompetencyCode, int Level);

/// <summary>One requirement of a target job against the employee's level. <see cref="Gap"/> is 0 when it is met.</summary>
internal sealed record RequirementProgress(string CompetencyCode, int RequiredLevel, int CurrentLevel, int Weight, bool Met, int Gap);

/// <summary>A job the employee could move to. <see cref="Steps"/> is the fewest career moves to get there, or null when they have no current job.</summary>
internal sealed record Opportunity(JobSummary Job, int? Steps, decimal MatchPercent, IReadOnlyList<RequirementProgress> Requirements);

/// <summary>Everything the employee Career Centre shows. Competency levels are personal data, so the caller needs Internal access to the profile.</summary>
internal sealed record CentreResponse(
    Guid EmployeeId, JobSummary? CurrentJob, IReadOnlyList<CompetencyLevelDto> Competencies, IReadOnlyList<Opportunity> Opportunities);

internal sealed record RequirementRequest(string? CompetencyCode, int? RequiredLevel, int? Weight);

internal sealed record JobRequest(
    string? Code, string? Title, int? Level, string? Description, bool? IsActive, IReadOnlyList<RequirementRequest>? Requirements);

internal sealed record AddPathRequest(Guid? FromJobId, Guid? ToJobId);

internal sealed record CompetencyRequest(string? CompetencyCode, int? Level);

internal sealed record ProfileRequest(Guid? CurrentJobId, IReadOnlyList<CompetencyRequest>? Competencies);

internal sealed class JobRequestValidator : AbstractValidator<JobRequest>
{
    public JobRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Level).NotNull().InclusiveBetween(1, 20);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Requirements).NotNull().WithMessage("Send the requirements, even if the list is empty.");
        RuleForEach(x => x.Requirements).ChildRules(r =>
        {
            r.RuleFor(x => x.CompetencyCode).Must(CompetencyCodes.IsValid).WithMessage("The competency code must be 2 to 50 characters: capital letters, digits and underscores.");
            r.RuleFor(x => x.RequiredLevel).NotNull().InclusiveBetween(Job.MinCompetencyLevel, Job.MaxCompetencyLevel);
            r.RuleFor(x => x.Weight).InclusiveBetween(1, Job.MaxWeight);
        });
    }
}

internal sealed class AddPathRequestValidator : AbstractValidator<AddPathRequest>
{
    public AddPathRequestValidator()
    {
        RuleFor(x => x.FromJobId).NotNull().NotEqual(Guid.Empty);
        RuleFor(x => x.ToJobId).NotNull().NotEqual(Guid.Empty);
    }
}

internal sealed class ProfileRequestValidator : AbstractValidator<ProfileRequest>
{
    public ProfileRequestValidator()
    {
        RuleFor(x => x.Competencies).NotNull().WithMessage("Send the competencies, even if the list is empty.");
        RuleForEach(x => x.Competencies).ChildRules(c =>
        {
            c.RuleFor(x => x.CompetencyCode).Must(CompetencyCodes.IsValid).WithMessage("The competency code must be 2 to 50 characters: capital letters, digits and underscores.");
            c.RuleFor(x => x.Level).NotNull().InclusiveBetween(Job.MinCompetencyLevel, Job.MaxCompetencyLevel);
        });
    }
}
