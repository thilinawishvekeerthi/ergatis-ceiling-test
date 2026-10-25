using Talent.Config;
using Talent.Shared;
using Talent.Workflow;

namespace Talent.Host;

internal static class TalentModules
{
    /// <summary>Registers every module. The same call can load Talent inside another host later.</summary>
    public static IServiceCollection AddTalentModules(this IServiceCollection services, IConfiguration configuration) =>
        services.AddTalentShared(configuration).AddConfigModule(configuration).AddWorkflowModule(configuration);

    public static IEndpointRouteBuilder MapTalentEndpoints(this IEndpointRouteBuilder app) =>
        app.MapConfigEndpoints().MapWorkflowEndpoints();
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
        await app.Services.InitializeWorkflowAsync(app.Lifetime.ApplicationStopping);
    }
}
