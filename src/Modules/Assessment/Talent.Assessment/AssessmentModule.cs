using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Assessment.Api;
using Talent.Assessment.Persistence;
using Talent.Security.Contracts;
using Talent.Shared;
using Talent.Shared.Events;
using Microsoft.AspNetCore.Routing;

namespace Talent.Assessment;

/// <summary>The only public entry point of the Assessment module (dotnet-modular-backend skill).</summary>
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
        services.AddScoped<AssessmentService>();
        services.AddScoped<AssessmentApiService>();
        services.AddScoped<IValidator<CreateCycleRequest>, CreateCycleRequestValidator>();
        services.AddScoped<IValidator<AssignReviewRequest>, AssignReviewRequestValidator>();
        services.AddScoped<IValidator<RateRequest>, RateRequestValidator>();
        services.AddScoped<IValidator<CalibrateRequest>, CalibrateRequestValidator>();
        DecorateScopeMemberSource(services);
        return services;
    }

    /// <summary>Wraps whichever member source is registered so far. Register Security before this module.</summary>
    private static void DecorateScopeMemberSource(IServiceCollection services)
    {
        var inner = services.LastOrDefault(d => d.ServiceType == typeof(IScopeMemberSource))
                    ?? throw new InvalidOperationException("Add the Security module before the Assessment module.");
        services.Remove(inner);
        services.AddScoped<IScopeMemberSource>(sp => new AssignedScopeMemberSource(
            sp.GetRequiredService<AssessmentDbContext>(),
            inner.ImplementationFactory is { } factory
                ? (IScopeMemberSource)factory(sp)
                : (IScopeMemberSource)ActivatorUtilities.CreateInstance(sp, inner.ImplementationType!)));
    }

    public static async Task InitializeAssessmentAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AssessmentDbContext>().Database.MigrateAsync(cancellationToken);
    }

    public static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        AssessmentEndpoints.Map(app);
        return app;
    }
}
