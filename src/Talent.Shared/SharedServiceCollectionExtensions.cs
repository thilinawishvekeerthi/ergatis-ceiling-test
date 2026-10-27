using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Shared.Security;
using Talent.Shared.Events;
using Talent.Shared.Persistence;

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
        services.AddScoped<ICurrentUser, AnonymousCurrentUser>();
        return services;
    }

    public static Task MigrateSharedAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        var scope = services.CreateAsyncScope();
        return MigrateAndDisposeAsync(scope, cancellationToken);
    }

    private static async Task MigrateAndDisposeAsync(AsyncServiceScope scope, CancellationToken cancellationToken)
    {
        await using (scope)
        {
            await scope.ServiceProvider.GetRequiredService<TalentSharedDbContext>().Database.MigrateAsync(cancellationToken);
        }
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services) =>
        services.AddSingleton(TimeProvider.System);
}
