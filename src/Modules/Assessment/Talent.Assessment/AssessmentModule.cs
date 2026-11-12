using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Assessment.Api;
using Talent.Assessment.NineBox;
using Talent.Assessment.Persistence;
using Talent.Config.Contracts;
using Talent.Shared;
using Talent.Shared.Events;

namespace Talent.Assessment;

/// <summary>The only public entry point of the Assessment module.</summary>
public static class AssessmentModule
{
    public static IServiceCollection AddAssessmentModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(SharedServiceCollectionExtensions.ConnectionStringName)
                         ?? throw new InvalidOperationException("Connection string 'Talent' is missing.");

        services.AddDbContext<AssessmentDbContext>((sp, o) =>
        {
            AssessmentDbContextFactory.Configure((DbContextOptionsBuilder<AssessmentDbContext>)o, connection);
            o.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });
        services.AddScoped<NineBoxConfigProvider>();
        services.AddScoped<NineBoxService>();
        return services;
    }

    /// <summary>Applies the Assessment migrations and publishes the default nine-box scales and grid (version 1 only).</summary>
    public static async Task InitializeAssessmentAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AssessmentDbContext>().Database.MigrateAsync(cancellationToken);

        var publisher = scope.ServiceProvider.GetRequiredService<IConfigDefaultPublisher>();
        var reader = scope.ServiceProvider.GetRequiredService<IConfigVersionReader>();

        await publisher.EnsurePublishedAsync(
            ConfigType.Scale, NineBoxDefaults.PerformanceScaleCode, "Performance (3 levels, nine-box)", NineBoxDefaults.PerformanceScale, cancellationToken);
        await publisher.EnsurePublishedAsync(
            ConfigType.Scale, NineBoxDefaults.PotentialScaleCode, "Potential (3 levels, nine-box)", NineBoxDefaults.PotentialScale, cancellationToken);

        var performance = await reader.GetLatestPublishedAsync(ConfigType.Scale, NineBoxDefaults.PerformanceScaleCode, cancellationToken)
                          ?? throw new InvalidOperationException("The performance scale was not published.");
        var potential = await reader.GetLatestPublishedAsync(ConfigType.Scale, NineBoxDefaults.PotentialScaleCode, cancellationToken)
                        ?? throw new InvalidOperationException("The potential scale was not published.");

        await publisher.EnsurePublishedAsync(
            ConfigType.Grid, NineBoxDefaults.GridCode, "Nine-box talent evaluation (BRD TM-10)",
            NineBoxDefaults.GridPayload(performance.Id, potential.Id), cancellationToken);
    }

    public static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        NineBoxEndpoints.Map(app);
        return app;
    }
}
