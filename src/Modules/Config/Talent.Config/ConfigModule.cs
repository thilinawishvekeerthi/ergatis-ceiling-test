using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Config.Contracts;
using Talent.Config.Defaults;
using Talent.Config.Persistence;
using Talent.Shared;
using Talent.Shared.Events;

namespace Talent.Config;

/// <summary>The only public entry point of the Config module (dotnet-modular-backend skill).</summary>
public static class ConfigModule
{
    public static IServiceCollection AddConfigModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(SharedServiceCollectionExtensions.ConnectionStringName)
                         ?? throw new InvalidOperationException("Connection string 'Talent' is missing.");

        services.AddDbContext<ConfigDbContext>((sp, o) =>
        {
            ConfigDbContextFactory.Configure((DbContextOptionsBuilder<ConfigDbContext>)o, connection);
            o.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });
        services.AddScoped<ConfigAdminService>();
        services.AddScoped<ConfigVersionReader>();
        services.AddScoped<IConfigVersionReader>(sp => sp.GetRequiredService<ConfigVersionReader>());
        services.AddScoped<IConfigVersionLocker>(sp => sp.GetRequiredService<ConfigVersionReader>());
        return services;
    }

    /// <summary>Applies the Config migrations and publishes the default scales and nine-box.</summary>
    public static async Task InitializeConfigAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConfigDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await ConfigDefaultsSeeder.EnsureAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>(), cancellationToken);
    }

    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
