using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Config.Contracts;
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

    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
