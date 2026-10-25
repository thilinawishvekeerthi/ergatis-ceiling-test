namespace Talent.Config.Contracts;

public sealed record ConfigVersionDto(
    Guid Id,
    Guid DefinitionId,
    ConfigType Type,
    string Code,
    int VersionNo,
    ConfigVersionStatus Status,
    string PayloadJson,
    string? Checksum,
    DateTimeOffset? PublishedAt);
