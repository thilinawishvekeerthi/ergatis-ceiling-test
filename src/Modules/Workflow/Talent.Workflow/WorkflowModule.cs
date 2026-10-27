using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Talent.Shared;
using Talent.Workflow.Api;
using Talent.Shared.Events;
using Talent.Workflow.Contracts;
using Talent.Workflow.Defaults;
using Talent.Workflow.Persistence;

namespace Talent.Workflow;

/// <summary>The only public entry point of the Workflow module.</summary>
public static class WorkflowModule
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(SharedServiceCollectionExtensions.ConnectionStringName)
                         ?? throw new InvalidOperationException("Connection string 'Talent' is missing.");

        services.AddOptions<WorkflowOptions>()
            .Bind(configuration.GetSection(WorkflowOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<WorkflowDbContext>((sp, o) =>
        {
            WorkflowDbContextFactory.Configure((DbContextOptionsBuilder<WorkflowDbContext>)o, connection);
            o.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddScoped<IActorResolver, ManagerActorResolver>();
        services.AddScoped<IActorResolver, RoleActorResolver>();
        services.AddScoped<IActorResolver, UserActorResolver>();
        // Managers come from the synced employee copy, people from Security. A host can register its own first.
        services.TryAddScoped<IManagerLookup, DirectoryManagerLookup>();
        services.TryAddScoped<IRoleMemberLookup, DirectoryRoleMemberLookup>();

        services.AddScoped<TaskPlanner>();
        services.AddScoped<TransitionExecutor>();
        services.AddScoped<WorkflowAdminService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<WorkflowApi>();
        services.AddScoped<SlaProcessor>();
        services.AddSingleton<SlaTickRunner>();

        if (configuration.GetValue($"{WorkflowOptions.Section}:{nameof(WorkflowOptions.SlaTickEnabled)}", true))
        {
            services.AddHostedService<SlaTickService>();
        }

        return services;
    }

    /// <summary>Applies the Workflow migrations and publishes the default flows.</summary>
    public static async Task InitializeWorkflowAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await WorkflowDefaultsSeeder.EnsureAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>(), cancellationToken);
    }

    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        WorkflowEndpoints.Map(app);
        return app;
    }
}
