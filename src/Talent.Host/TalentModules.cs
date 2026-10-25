using Talent.Config;

namespace Talent.Host;

internal static class TalentModules
{
    /// <summary>Registers every module. The same call can load Talent inside another host later.</summary>
    public static IServiceCollection AddTalentModules(this IServiceCollection services, IConfiguration configuration) =>
        services.AddConfigModule(configuration);

    public static IEndpointRouteBuilder MapTalentEndpoints(this IEndpointRouteBuilder app) =>
        app.MapConfigEndpoints();
}
