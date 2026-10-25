using Microsoft.EntityFrameworkCore;
using Talent.Integration.Canonical;
using Talent.Integration.Contracts;
using Talent.Integration.Persistence;

namespace Talent.Integration.Sync;

/// <summary>
/// Idempotent writes with <c>INSERT ... ON CONFLICT</c>. Safe to repeat and safe when two syncs race.
/// An older source version never overwrites a newer one.
/// </summary>
internal sealed class CanonicalWriter(IntegrationDbContext db, TimeProvider clock)
{
    public async Task<Guid?> ResolveAsync(string system, string entity, string? externalId, CancellationToken ct)
    {
        if (externalId is null)
        {
            return null;
        }

        var id = await db.IdMap.AsNoTracking()
            .Where(m => m.System == system && m.Entity == entity && m.ExternalId == externalId)
            .Select(m => (Guid?)m.InternalId)
            .SingleOrDefaultAsync(ct);
        return id ?? throw new UnresolvedReferenceException(entity, externalId);
    }

    /// <summary>Returns the Talent id for the source key, creating the mapping on first sight.</summary>
    private async Task<Guid> ClaimIdAsync(string system, string entity, string externalId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var candidate = Guid.CreateVersion7();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO integration.external_id_map (system, entity, external_id, internal_id, created_at)
            VALUES ({system}, {entity}, {externalId}, {candidate}, {now})
            ON CONFLICT (system, entity, external_id) DO NOTHING
            """, ct);
        return await db.IdMap.AsNoTracking()
            .Where(m => m.System == system && m.Entity == entity && m.ExternalId == externalId)
            .Select(m => m.InternalId)
            .SingleAsync(ct);
    }

    public Task UpsertAsync(string system, SourceRecord<OrgUnitSource> record, Guid? parentId, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            var id = await ClaimIdAsync(system, EntityNames.OrgUnit, record.ExternalId, ct);
            var p = record.Payload;
            var now = clock.GetUtcNow();
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO integration.org_units (id, code, name, parent_id, is_active, source_modified_at, synced_at)
                VALUES ({id}, {p.Code}, {p.Name}, {parentId}::uuid, {p.IsActive}, {record.ModifiedAt}, {now})
                ON CONFLICT (id) DO UPDATE SET
                    code = EXCLUDED.code, name = EXCLUDED.name, parent_id = EXCLUDED.parent_id,
                    is_active = EXCLUDED.is_active, source_modified_at = EXCLUDED.source_modified_at, synced_at = EXCLUDED.synced_at
                WHERE integration.org_units.source_modified_at <= EXCLUDED.source_modified_at
                """, ct);
        }, ct);

    public Task UpsertAsync(string system, SourceRecord<PositionSource> record, Guid? orgUnitId, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            var id = await ClaimIdAsync(system, EntityNames.Position, record.ExternalId, ct);
            var p = record.Payload;
            var now = clock.GetUtcNow();
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO integration.positions (id, code, title, org_unit_id, is_active, source_modified_at, synced_at)
                VALUES ({id}, {p.Code}, {p.Title}, {orgUnitId}::uuid, {p.IsActive}, {record.ModifiedAt}, {now})
                ON CONFLICT (id) DO UPDATE SET
                    code = EXCLUDED.code, title = EXCLUDED.title, org_unit_id = EXCLUDED.org_unit_id,
                    is_active = EXCLUDED.is_active, source_modified_at = EXCLUDED.source_modified_at, synced_at = EXCLUDED.synced_at
                WHERE integration.positions.source_modified_at <= EXCLUDED.source_modified_at
                """, ct);
        }, ct);

    public Task UpsertAsync(
        string system, SourceRecord<EmployeeSource> record, Guid? orgUnitId, Guid? positionId, Guid? managerId, CancellationToken ct) =>
        InTransactionAsync(async () =>
        {
            var id = await ClaimIdAsync(system, EntityNames.Employee, record.ExternalId, ct);
            var p = record.Payload;
            var now = clock.GetUtcNow();
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO integration.employees
                    (id, employee_number, first_name, last_name, email, org_unit_id, position_id, manager_id, hire_date, status, source_modified_at, synced_at)
                VALUES ({id}, {p.EmployeeNumber}, {p.FirstName}, {p.LastName}, {p.Email}::text, {orgUnitId}::uuid, {positionId}::uuid,
                        {managerId}::uuid, {p.HireDate}::date, {p.Status}, {record.ModifiedAt}, {now})
                ON CONFLICT (id) DO UPDATE SET
                    employee_number = EXCLUDED.employee_number, first_name = EXCLUDED.first_name, last_name = EXCLUDED.last_name,
                    email = EXCLUDED.email, org_unit_id = EXCLUDED.org_unit_id, position_id = EXCLUDED.position_id,
                    manager_id = EXCLUDED.manager_id, hire_date = EXCLUDED.hire_date, status = EXCLUDED.status,
                    source_modified_at = EXCLUDED.source_modified_at, synced_at = EXCLUDED.synced_at
                WHERE integration.employees.source_modified_at <= EXCLUDED.source_modified_at
                """, ct);
        }, ct);

    // The id mapping and the canonical row commit together, so a rejected row leaves no mapping behind.
    private async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await work();
        await tx.CommitAsync(ct);
    }
}
