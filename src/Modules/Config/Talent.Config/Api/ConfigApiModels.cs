using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using Talent.Config.Contracts;
using Talent.Config.Versions;
using Talent.Security.Contracts;

namespace Talent.Config.Api;

/// <summary>
/// What the API returns for one config version. Field classes drive server-side masking:
/// the payload, checksum and publisher are Internal, so a role with Public access sees only the identity and status.
/// </summary>
internal sealed record ConfigVersionResponse(
    [property: FieldClassification(FieldClass.Public)] Guid Id,
    [property: FieldClassification(FieldClass.Public)] Guid DefinitionId,
    [property: FieldClassification(FieldClass.Public)] string Type,
    [property: FieldClassification(FieldClass.Public)] string Code,
    [property: FieldClassification(FieldClass.Public)] int VersionNo,
    [property: FieldClassification(FieldClass.Public)] string Status,
    [property: FieldClassification(FieldClass.Internal)] JsonElement? Payload,
    [property: FieldClassification(FieldClass.Internal)] string? Checksum,
    [property: FieldClassification(FieldClass.Internal)] string? PublishedBy,
    [property: FieldClassification(FieldClass.Public)] DateTimeOffset? PublishedAt)
{
    public static ConfigVersionResponse From(ConfigDefinition definition, ConfigVersion version) => new(
        version.Id, definition.Id, definition.Type.ToString(), definition.Code, version.VersionNo, version.Status.ToString(),
        JsonDocument.Parse(version.PayloadJson).RootElement.Clone(), version.Checksum, version.PublishedBy, version.PublishedAt);
}

/// <summary>One row of the admin list: a definition and its newest version, whatever its status.</summary>
internal sealed record ConfigSummaryResponse(
    [property: FieldClassification(FieldClass.Public)] Guid DefinitionId,
    [property: FieldClassification(FieldClass.Public)] string Type,
    [property: FieldClassification(FieldClass.Public)] string Code,
    [property: FieldClassification(FieldClass.Public)] string Name,
    [property: FieldClassification(FieldClass.Public)] Guid LatestVersionId,
    [property: FieldClassification(FieldClass.Public)] int VersionNo,
    [property: FieldClassification(FieldClass.Public)] string Status);

/// <summary>Create a draft. For a new definition both fields are required. For an existing one, an empty payload copies the newest version.</summary>
internal sealed record CreateDraftRequest(string? Name, JsonElement? Payload);

internal sealed record UpdatePayloadRequest(JsonElement? Payload);

internal sealed record ConfigRoute(string Type, string Code)
{
    public ConfigType ConfigType => Enum.Parse<ConfigType>(Type, ignoreCase: true);
}

internal sealed partial class ConfigRouteValidator : AbstractValidator<ConfigRoute>
{
    public ConfigRouteValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => Enum.GetNames<ConfigType>().Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Unknown config type.");
        RuleFor(x => x.Code)
            .Must(c => CodePattern().IsMatch(c))
            .WithMessage("The code must be 2 to 50 characters: capital letters, digits and underscores, starting with a letter.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex CodePattern();
}

internal sealed class CreateDraftRequestValidator : AbstractValidator<CreateDraftRequest>
{
    public CreateDraftRequestValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Payload)
            .Must(p => p is null || p.Value.ValueKind == JsonValueKind.Object)
            .WithMessage("The payload must be a JSON object.");
    }
}

internal sealed class UpdatePayloadRequestValidator : AbstractValidator<UpdatePayloadRequest>
{
    public UpdatePayloadRequestValidator()
    {
        RuleFor(x => x.Payload)
            .Must(p => p is { ValueKind: JsonValueKind.Object })
            .WithMessage("The payload must be a JSON object.");
    }
}
