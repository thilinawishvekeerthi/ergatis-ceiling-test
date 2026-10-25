using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Config.Versions;
using Talent.Shared.Auditing;

namespace Talent.Config;

internal sealed class ConfigVersionReader(ConfigDbContext db, TimeProvider clock) : IConfigVersionReader, IConfigVersionLocker
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
        db.AddAudit(new Talent.Shared.Security.SystemCurrentUser(), clock, "Lock", nameof(ConfigVersion), versionId.ToString());
        await db.SaveChangesAsync(cancellationToken);
    }
}
