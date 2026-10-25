namespace Talent.Integration.Sync;

/// <summary>The source record is wrong. It goes to the dead-letter table.</summary>
internal sealed class InvalidSourceRecordException(string message) : Exception(message);

/// <summary>A referenced record has not been synced (yet). The record waits for a later pass.</summary>
internal sealed class UnresolvedReferenceException(string entity, string externalId)
    : Exception($"{entity} '{externalId}' is not known")
{
    public string Entity { get; } = entity;

    public string ExternalId { get; } = externalId;
}
