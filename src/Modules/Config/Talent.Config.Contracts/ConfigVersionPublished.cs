using Talent.Shared.Events;

namespace Talent.Config.Contracts;

public sealed record ConfigVersionPublished(
    DateTimeOffset OccurredAt,
    Guid VersionId,
    Guid DefinitionId,
    ConfigType Type,
    string Code,
    int VersionNo) : DomainEvent(OccurredAt);
