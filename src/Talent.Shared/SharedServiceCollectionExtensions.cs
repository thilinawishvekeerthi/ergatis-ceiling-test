using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Shared.Events;
using Talent.Shared.Persistence;
using Talent.Shared.Security;

namespace Talent.Shared;

public static class SharedServiceCollectionExtensions
{
    public const string ConnectionStringName = "Talent";

    public static IServiceCollection AddTalentShared(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(ConnectionStringName)
                         ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

        services.TryAddSingletonTimeProvider();
        services.AddSingleton<OutboxSaveChangesInterceptor>();
        services.AddDbContext<TalentSharedDbContext>(o =>
            o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__ef_migrations", SharedModel.Schema)));
        services.AddScoped<OutboxDispatcher>();
        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services) =>
        services.AddSingleton(TimeProvider.System);
}
