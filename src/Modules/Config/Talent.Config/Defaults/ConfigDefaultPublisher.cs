using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Shared.Security;

namespace Talent.Config.Defaults;

internal sealed class ConfigDefaultPublisher(ConfigDbContext db, TimeProvider clock) : IConfigDefaultPublisher
{
    public async Task EnsurePublishedAsync(ConfigType type, string code, string name, string payloadJson, CancellationToken cancellationToken)
    {
        var admin = new ConfigAdminService(db, new SystemCurrentUser(), clock);
        await ConfigDefaultsSeeder.EnsureAsync(db, admin, type, code, name, payloadJson, cancellationToken);
    }
}
