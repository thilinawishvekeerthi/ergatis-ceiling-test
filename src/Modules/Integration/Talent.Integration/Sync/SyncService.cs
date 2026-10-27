using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Talent.Integration.Adapters;
using Talent.Integration.Canonical;
using Talent.Integration.Contracts;
using Talent.Integration.Persistence;

namespace Talent.Integration.Sync;

/// <summary>
/// Delta sync (ADR-08): adapter, validate, map ids, idempotent upsert. A bad record is dead-lettered and the batch goes on.
/// Logs carry ids and counts only, never source payloads.
/// </summary>
internal sealed partial class SyncService(
    IntegrationDbContext db,
    ISourceAdapter<OrgUnitSource> orgUnits,
    ISourceAdapter<PositionSource> positions,
    ISourceAdapter<EmployeeSource> employees,
    TimeProvider clock,
    ILogger<SyncService> logger) : ISyncService
{
    // The source clock and ours differ a little. Re-reading a few minutes is harmless because upserts are idempotent.
    private static readonly TimeSpan Overlap = TimeSpan.FromMinutes(5);

    private readonly CanonicalWriter _writer = new(db, clock);

    public async Task<SyncResult> RunDeltaAsync(CancellationToken cancellationToken)
    {
        var started = clock.GetUtcNow();

        // Parents before children: org units, then positions, then employees.
        var org = await SyncEntityAsync(EntityNames.OrgUnit, orgUnits, SourceValidator.Validate, async (r, ct) =>
            await _writer.UpsertAsync(orgUnits.System, r, await _writer.ResolveAsync(orgUnits.System, EntityNames.OrgUnit, r.Payload.ParentCode, ct), ct),
            started, cancellationToken);

        var pos = await SyncEntityAsync(EntityNames.Position, positions, SourceValidator.Validate, async (r, ct) =>
            await _writer.UpsertAsync(positions.System, r, await _writer.ResolveAsync(positions.System, EntityNames.OrgUnit, r.Payload.OrgUnitCode, ct), ct),
            started, cancellationToken);

        var emp = await SyncEntityAsync(EntityNames.Employee, employees, SourceValidator.Validate, async (r, ct) =>
        {
            var system = employees.System;
            var unit = await _writer.ResolveAsync(system, EntityNames.OrgUnit, r.Payload.OrgUnitCode, ct);
            var position = await _writer.ResolveAsync(system, EntityNames.Position, r.Payload.PositionCode, ct);
            var manager = await _writer.ResolveAsync(system, EntityNames.Employee, r.Payload.ManagerEmployeeNumber, ct);
            await _writer.UpsertAsync(system, r, unit, position, manager, ct);
        }, started, cancellationToken);

        return new SyncResult(orgUnits.System, [org, pos, emp]);
    }

    private async Task<SyncEntityResult> SyncEntityAsync<T>(
        string entity,
        ISourceAdapter<T> adapter,
        Func<T, string?> validate,
        Func<SourceRecord<T>, CancellationToken, Task> apply,
        DateTimeOffset started,
        CancellationToken ct)
        where T : class
    {
        var system = adapter.System;
        var watermark = await db.SyncStates.AsNoTracking()
            .Where(s => s.System == system && s.Entity == entity)
            .Select(s => (DateTimeOffset?)s.LastSuccessAt)
            .SingleOrDefaultAsync(ct);
        var since = watermark is { } w ? w - Overlap : DateTimeOffset.MinValue;

        var pending = new Dictionary<string, SourceRecord<T>>();
        await foreach (var record in adapter.FetchChangesAsync(since, ct))
        {
            pending[record.ExternalId] = record;
        }

        var fetched = pending.Count;

        // Rows that failed before get another chance, even when the source did not touch them again.
        var open = await db.DeadLetters.AsNoTracking()
            .Where(d => d.System == system && d.Entity == entity && d.ResolvedAt == null)
            .Select(d => d.ExternalId)
            .ToListAsync(ct);
        var retrying = new HashSet<string>(open);
        foreach (var externalId in open.Where(id => !pending.ContainsKey(id)))
        {
            if (await adapter.FetchOneAsync(externalId, ct) is { } again)
            {
                pending[externalId] = again;
            }
        }

        var upserted = 0;
        var recovered = 0;
        var deadLettered = 0;
        var queue = pending.Values.ToList();

        // Repeat while progress is made, so a child that arrives before its parent still lands.
        while (queue.Count > 0)
        {
            var deferred = new List<(SourceRecord<T> Record, UnresolvedReferenceException Reason)>();
            foreach (var record in queue)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var error = validate(record.Payload);
                    if (error is not null)
                    {
                        throw new InvalidSourceRecordException(error);
                    }

                    await apply(record, ct);
                    upserted++;
                    if (retrying.Contains(record.ExternalId))
                    {
                        recovered++;
                        await ResolveDeadLetterAsync(system, entity, record.ExternalId, ct);
                    }
                }
                catch (UnresolvedReferenceException ex)
                {
                    deferred.Add((record, ex));
                }
                catch (Exception ex) when (ex is InvalidSourceRecordException or DbUpdateException or NpgsqlException or PostgresException)
                {
                    deadLettered++;
                    await DeadLetterAsync(system, entity, record, ex.GetBaseException().Message, ct);
                }
            }

            if (deferred.Count == queue.Count)
            {
                foreach (var (record, reason) in deferred)
                {
                    deadLettered++;
                    await DeadLetterAsync(system, entity, record, reason.Message, ct);
                }

                break;
            }

            queue = [.. deferred.Select(d => d.Record)];
        }

        await SaveWatermarkAsync(system, entity, started, ct);
        LogEntitySynced(logger, system, entity, fetched, upserted, deadLettered);
        return new SyncEntityResult(entity, fetched, upserted, deadLettered, recovered);
    }

    private async Task SaveWatermarkAsync(string system, string entity, DateTimeOffset at, CancellationToken ct) =>
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO integration.sync_state (system, entity, last_success_at) VALUES ({system}, {entity}, {at})
            ON CONFLICT (system, entity) DO UPDATE SET last_success_at = GREATEST(integration.sync_state.last_success_at, EXCLUDED.last_success_at)
            """, ct);

    private async Task ResolveDeadLetterAsync(string system, string entity, string externalId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.DeadLetters
            .Where(d => d.System == system && d.Entity == entity && d.ExternalId == externalId && d.ResolvedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ResolvedAt, now), ct);
    }

    private async Task DeadLetterAsync<T>(string system, string entity, SourceRecord<T> record, string error, CancellationToken ct)
        where T : class
    {
        var now = clock.GetUtcNow();
        var id = Guid.CreateVersion7();
        var payload = JsonSerializer.Serialize(record.Payload);
        var message = error.Length > 1000 ? error[..1000] : error;
        LogDeadLettered(logger, system, entity, record.ExternalId);
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO integration.dead_letters (id, system, entity, external_id, payload_json, error, attempts, first_failed_at, last_failed_at)
            VALUES ({id}, {system}, {entity}, {record.ExternalId}, {payload}::jsonb, {message}, 1, {now}, {now})
            ON CONFLICT (system, entity, external_id) WHERE resolved_at IS NULL DO UPDATE SET
                payload_json = EXCLUDED.payload_json, error = EXCLUDED.error,
                attempts = integration.dead_letters.attempts + 1, last_failed_at = EXCLUDED.last_failed_at
            """, ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Synced {System}/{Entity}: fetched {Fetched}, upserted {Upserted}, dead-lettered {DeadLettered}")]
    private static partial void LogEntitySynced(ILogger logger, string system, string entity, int fetched, int upserted, int deadLettered);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead-lettered {System}/{Entity} record {ExternalId}")]
    private static partial void LogDeadLettered(ILogger logger, string system, string entity, string externalId);
}
