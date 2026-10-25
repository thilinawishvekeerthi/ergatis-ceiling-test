using System.Security.Cryptography;
using System.Text;
using Talent.Config.Contracts;
using Talent.Shared.Events;
using Talent.Workflow.Contracts;
using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;

namespace Talent.Workflow.Versions;

/// <summary>
/// One version of a workflow definition with the ADR-02 lifecycle: Draft, Published, Locked, Retired.
/// Locked when the first instance starts on it. Running instances finish on the version they started with.
/// </summary>
internal sealed class WorkflowVersion : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    private WorkflowVersion() => PayloadJson = "";

    private WorkflowVersion(Guid definitionId, int versionNo, string payloadJson)
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

    public static WorkflowVersion CreateDraft(Guid definitionId, int versionNo, string payloadJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNo, 1);
        return new WorkflowVersion(definitionId, versionNo, payloadJson);
    }

    public void UpdatePayload(string payloadJson)
    {
        RequireStatus("edit", ConfigVersionStatus.Draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        PayloadJson = payloadJson;
    }

    /// <summary>Validates the definition, then freezes it with a checksum of its canonical form.</summary>
    public void Publish(string definitionCode, string publishedBy, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publishedBy);
        RequireStatus("publish", ConfigVersionStatus.Draft);

        var canonical = WorkflowDefinitionModel.Parse(PayloadJson).ToCanonicalJson();
        PayloadJson = canonical;
        Checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        PublishedBy = publishedBy;
        PublishedAt = now;
        Status = ConfigVersionStatus.Published;
        _events.Add(new ConfigVersionPublished(now, Id, DefinitionId, ConfigType.Workflow, definitionCode, VersionNo));
    }

    /// <summary>Called when the first instance starts on this version. Idempotent.</summary>
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

    public WorkflowVersion CreateNextVersion()
    {
        RequireStatus("copy", ConfigVersionStatus.Published, ConfigVersionStatus.Locked, ConfigVersionStatus.Retired);
        return new WorkflowVersion(DefinitionId, VersionNo + 1, PayloadJson);
    }

    private void RequireStatus(string action, params ConfigVersionStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new WorkflowConflictException($"Cannot {action} a workflow version that is {Status}.");
        }
    }
}
