using System.Security.Cryptography;
using System.Text;
using Talent.Config.Contracts;
using Talent.Shared.Events;

namespace Talent.Config.Versions;

/// <summary>
/// One immutable-once-published version of a configurable object (ADR-02).
/// Draft is the only editable state. A change to used config is always a new version.
/// </summary>
internal sealed class ConfigVersion : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    // For EF Core.
    private ConfigVersion() => PayloadJson = "";

    private ConfigVersion(Guid definitionId, int versionNo, string payloadJson)
    {
        Id = Guid.NewGuid();
        DefinitionId = definitionId;
        VersionNo = versionNo;
        PayloadJson = payloadJson;
        Status = ConfigVersionStatus.Draft;
    }

    public Guid Id { get; private set; }
    public Guid DefinitionId { get; private set; }
    public int VersionNo { get; private set; }
    public ConfigVersionStatus Status { get; private set; }
    public string PayloadJson { get; private set; }
    public string? Checksum { get; private set; }
    public string? PublishedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();

    internal void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    public static ConfigVersion CreateDraft(Guid definitionId, int versionNo, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        return new ConfigVersion(definitionId, versionNo, payloadJson);
    }

    public void UpdatePayload(string payloadJson)
    {
        if (Status != ConfigVersionStatus.Draft)
        {
            throw new ConfigVersionLockedException(Id);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        PayloadJson = payloadJson;
    }

    public void Publish(string publishedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publishedBy);
        RequireStatus("publish", ConfigVersionStatus.Draft);

        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson.Canonicalize(PayloadJson))));
        Checksum = checksum;
        PublishedBy = publishedBy;
        PublishedAt = now;
        Status = ConfigVersionStatus.Published;
    }

    /// <summary>Called when a cycle or record first references this version. Idempotent.</summary>
    public void Lock()
    {
        if (Status == ConfigVersionStatus.Locked)
        {
            return;
        }

        RequireStatus("lock", ConfigVersionStatus.Published);
        Status = ConfigVersionStatus.Locked;
    }

    public void Retire()
    {
        RequireStatus("retire", ConfigVersionStatus.Published, ConfigVersionStatus.Locked);
        Status = ConfigVersionStatus.Retired;
    }

    /// <summary>Copies this version into a new draft with the next version number.</summary>
    public ConfigVersion CreateNextVersion()
    {
        RequireStatus("copy", ConfigVersionStatus.Published, ConfigVersionStatus.Locked, ConfigVersionStatus.Retired);
        return new ConfigVersion(DefinitionId, VersionNo + 1, PayloadJson);
    }

    private void RequireStatus(string action, params ConfigVersionStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidConfigTransitionException(action, Status.ToString());
        }
    }
}
