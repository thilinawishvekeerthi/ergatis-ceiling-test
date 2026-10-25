using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Shared.Security;

namespace Talent.Config.Defaults;

internal sealed class ConfigDefaultPublisher(ConfigDbContext db, TimeProvider clock, IEnumerable<IConfigPayloadValidator> validators) : IConfigDefaultPublisher
{
    public async Task EnsurePublishedAsync(ConfigType type, string code, string name, string payloadJson, CancellationToken cancellationToken)
    {
        var admin = new ConfigAdminService(db, new SystemCurrentUser(), clock, validators);
        await ConfigDefaultsSeeder.EnsureAsync(db, admin, type, code, name, payloadJson, cancellationToken);
    }
}
