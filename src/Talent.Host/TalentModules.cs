using Talent.Config;
using Talent.Security;
using Talent.Integration;
using Talent.Shared;
using Talent.Workflow;

namespace Talent.Host;

internal static class TalentModules
{
    /// <summary>Registers every module. The same call can load Talent inside another host later.</summary>
    public static IServiceCollection AddTalentModules(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTalentShared(configuration).AddConfigModule(configuration).AddSecurityModule(configuration).AddIntegrationModule(configuration).AddWorkflowModule(configuration);

    public static IEndpointRouteBuilder MapTalentEndpoints(this IEndpointRouteBuilder app) =>
        app.MapSecurityEndpoints().MapConfigEndpoints().MapIntegrationEndpoints().MapWorkflowEndpoints();
}

internal static class TalentStartup
{
    /// <summary>Migrates and seeds when <c>Database:MigrateOnStartup</c> is true (dev and tests). Production runs migrations as a deploy step.</summary>
    public static async Task InitializeAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await app.Services.MigrateSharedAsync(app.Lifetime.ApplicationStopping);
        await app.Services.InitializeConfigAsync(app.Lifetime.ApplicationStopping);
        // Integration before Security: development seed users are linked to synced employees by employee number.
        await app.Services.InitializeIntegrationAsync(app.Lifetime.ApplicationStopping);
        await app.Services.InitializeSecurityAsync(app.Lifetime.ApplicationStopping);
        await app.Services.InitializeWorkflowAsync(app.Lifetime.ApplicationStopping);
    }
}
