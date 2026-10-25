namespace Talent.Shared.Auditing;

/// <summary>One row of the append-only audit log (ADR-07). Updates and deletes are blocked by the database.</summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string ObjectType { get; set; } = "";
    public string ObjectId { get; set; } = "";
    public bool Confidential { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
