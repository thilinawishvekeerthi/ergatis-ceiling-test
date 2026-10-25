using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Talent.Integration.Adapters;
using Talent.Integration.Contracts;
using Talent.Integration.Persistence;
using Talent.Integration.Readers;
using Talent.Integration.Sync;
using Talent.Shared;
using Talent.Shared.Events;

namespace Talent.Integration;

/// <summary>The only public entry point of the Integration module.</summary>
public static class IntegrationModule
{
    public static IServiceCollection AddIntegrationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(SharedServiceCollectionExtensions.ConnectionStringName)
                         ?? throw new InvalidOperationException("Connection string 'Talent' is missing.");

        services.AddDbContext<IntegrationDbContext>((sp, o) =>
        {
            IntegrationDbContextFactory.Configure((DbContextOptionsBuilder<IntegrationDbContext>)o, connection);
            o.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddOptions<SyncOptions>().Bind(configuration.GetSection(SyncOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SourceSystemOptions>().Bind(configuration.GetSection("Integration:CORE_HR"));

        AddCoreHrAdapters(services, configuration.GetSection("Integration:CORE_HR").Get<SourceSystemOptions>() ?? new());

        services.AddScoped<SyncService>();
        services.AddScoped<ISyncService>(sp => sp.GetRequiredService<SyncService>());
        services.AddScoped<NightlySyncJob>();
        services.AddScoped<IEmployeeReader, EmployeeReader>();
        services.AddScoped<IOrgUnitReader, OrgUnitReader>();
        services.AddScoped<IPositionReader, PositionReader>();
        services.AddScoped<IDeadLetterReader, DeadLetterReader>();

        var sync = configuration.GetSection(SyncOptions.Section).Get<SyncOptions>() ?? new();
        if (sync.Scheduler == SyncScheduler.Hangfire)
        {
            services.AddHangfire(c => c
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connection)));
            services.AddHangfireServer();
        }

        return services;
    }

    private static void AddCoreHrAdapters(IServiceCollection services, SourceSystemOptions options)
    {
        if (!string.Equals(options.Mode, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Integration:CORE_HR:Mode '{options.Mode}' is not available yet. Supported: Mock.");
        }

        services.AddSingleton(sp => new MockSourceData(options.Mock.Seed, options.Mock.Employees, sp.GetRequiredService<TimeProvider>().GetUtcNow()));
        services.AddSingleton<ISourceAdapter<OrgUnitSource>, MockOrgUnitAdapter>();
        services.AddSingleton<ISourceAdapter<PositionSource>, MockPositionAdapter>();
        services.AddSingleton<ISourceAdapter<EmployeeSource>, MockEmployeeAdapter>();
    }

    /// <summary>Applies the Integration migrations and, when configured, registers the nightly Hangfire job.</summary>
    public static async Task InitializeIntegrationAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var sync = scope.ServiceProvider.GetRequiredService<IOptions<SyncOptions>>().Value;
        await scope.ServiceProvider.GetRequiredService<IntegrationDbContext>().Database.MigrateAsync(cancellationToken);

        if (sync.Scheduler == SyncScheduler.Hangfire)
        {
            scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<NightlySyncJob>(
                NightlySyncJob.JobId,
                job => job.RunAsync(CancellationToken.None),
                sync.Cron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        }
    }

    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
