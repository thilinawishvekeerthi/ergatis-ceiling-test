namespace Talent.Shared.Persistence;

/// <summary>A domain event saved in the same transaction as the change that raised it (ADR-10).</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

/// <summary>Records that a handler already processed an event, so a second delivery does nothing.</summary>
public sealed class ProcessedEvent
{
    public string Handler { get; set; } = "";
    public Guid EventId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
