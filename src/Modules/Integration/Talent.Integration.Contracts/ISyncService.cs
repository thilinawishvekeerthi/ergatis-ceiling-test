namespace Talent.Integration.Contracts;

public sealed record SyncEntityResult(string Entity, int Fetched, int Upserted, int DeadLettered, int Recovered);

public sealed record SyncResult(string System, IReadOnlyList<SyncEntityResult> Entities)
{
    public int TotalDeadLettered => Entities.Sum(e => e.DeadLettered);
}

public sealed record DeadLetterDto(
    Guid Id,
    string System,
    string Entity,
    string ExternalId,
    string Error,
    int Attempts,
    DateTimeOffset FirstFailedAt,
    DateTimeOffset LastFailedAt);

/// <summary>Runs the delta sync. The only caller from a user request is the HR admin refresh action.</summary>
public interface ISyncService
{
    /// <summary>Nightly delta: org units, then positions, then employees. Failed rows are dead-lettered, the batch continues.</summary>
    Task<SyncResult> RunDeltaAsync(CancellationToken cancellationToken);
}

/// <summary>Feeds the admin dashboard.</summary>
public interface IDeadLetterReader
{
    Task<IReadOnlyList<DeadLetterDto>> ListOpenAsync(CancellationToken cancellationToken);
}
