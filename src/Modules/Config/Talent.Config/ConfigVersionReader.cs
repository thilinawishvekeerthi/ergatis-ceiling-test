using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Config.Versions;
using Talent.Shared.Auditing;
using Talent.Shared.Security;

namespace Talent.Config;

internal sealed class ConfigVersionReader(ConfigDbContext db, TimeProvider clock, ICurrentUser user) : IConfigVersionReader, IConfigVersionLocker
{
    public async Task<ConfigVersionDto?> GetAsync(Guid versionId, CancellationToken cancellationToken) =>
        await (from v in db.Versions
               join d in db.Definitions on v.DefinitionId equals d.Id
               where v.Id == versionId
               select new ConfigVersionDto(
                   v.Id, v.DefinitionId, d.Type, d.Code, v.VersionNo, v.Status, v.PayloadJson, v.Checksum, v.PublishedAt))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ConfigVersionDto?> GetLatestPublishedAsync(ConfigType type, string code, CancellationToken cancellationToken) =>
        await (from v in db.Versions
               join d in db.Definitions on v.DefinitionId equals d.Id
               where d.Type == type && d.Code == code
                     && (v.Status == ConfigVersionStatus.Published || v.Status == ConfigVersionStatus.Locked)
               orderby v.VersionNo descending
               select new ConfigVersionDto(
                   v.Id, v.DefinitionId, d.Type, d.Code, v.VersionNo, v.Status, v.PayloadJson, v.Checksum, v.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task LockAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await db.Versions.SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken)
                      ?? throw new ConfigNotFoundException("Config version", versionId);

        if (version.Status == ConfigVersionStatus.Locked)
        {
            return;
        }

        version.Lock();
        // A background caller has no signed-in user, so the audit trail names the system.
        db.AddAudit(user.IsAuthenticated ? user : new SystemCurrentUser(), clock, "Lock", nameof(ConfigVersion), versionId.ToString());

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another caller changed the row first. Locking is idempotent, so succeed if it is now locked.
            db.ChangeTracker.Clear();
            var current = await db.Versions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
            if (current?.Status != ConfigVersionStatus.Locked)
            {
                throw;
            }
        }
    }
}
