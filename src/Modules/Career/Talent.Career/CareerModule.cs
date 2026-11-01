using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Career.Api;
using Talent.Career.Persistence;
using Talent.Shared;
using Talent.Shared.Events;

namespace Talent.Career;

/// <summary>The only public entry point of the Career module (dotnet-modular-backend skill).</summary>
public static class CareerModule
{
    public static IServiceCollection AddCareerModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(SharedServiceCollectionExtensions.ConnectionStringName)
                         ?? throw new InvalidOperationException("Connection string 'Talent' is missing.");

        services.AddDbContext<CareerDbContext>((sp, o) =>
        {
            CareerDbContextFactory.Configure((DbContextOptionsBuilder<CareerDbContext>)o, connection);
            o.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });
        services.AddScoped<CareerService>();
        services.AddScoped<CareerApiService>();
        services.AddScoped<IValidator<JobRequest>, JobRequestValidator>();
        services.AddScoped<IValidator<AddPathRequest>, AddPathRequestValidator>();
        services.AddScoped<IValidator<ProfileRequest>, ProfileRequestValidator>();
        return services;
    }

    public static async Task InitializeCareerAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CareerDbContext>().Database.MigrateAsync(cancellationToken);
    }

    public static IEndpointRouteBuilder MapCareerEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        CareerEndpoints.Map(app);
        return app;
    }
}
