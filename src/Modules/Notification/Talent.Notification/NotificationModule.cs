using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Talent.Assessment.Contracts;
using Talent.Career.Contracts;
using Talent.Notification.Api;
using Talent.Notification.Contracts;
using Talent.Notification.Email;
using Talent.Notification.Handlers;
using Talent.Notification.Persistence;
using Talent.Shared;
using Talent.Shared.Events;

namespace Talent.Notification;

/// <summary>The only public entry point of the Notification module (dotnet-modular-backend skill).</summary>
public static class NotificationModule
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connection = configuration.GetConnectionString(SharedServiceCollectionExtensions.ConnectionStringName)
                         ?? throw new InvalidOperationException("Connection string 'Talent' is missing.");

        services.AddDbContext<NotificationDbContext>((sp, o) =>
        {
            NotificationDbContextFactory.Configure((DbContextOptionsBuilder<NotificationDbContext>)o, connection);
            o.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
        });

        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.Section)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
        var email = configuration.GetSection(EmailOptions.Section).Get<EmailOptions>() ?? new();
        if (email.IsSmtp)
        {
            services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.TryAddSingleton<IEmailSender, LogEmailSender>();
        }

        services.AddScoped<NotificationEngine>();
        services.AddScoped<INotifier>(sp => sp.GetRequiredService<NotificationEngine>());
        services.AddScoped<EmailDeliveryService>();
        if (email.PollSeconds > 0)
        {
            services.AddHostedService<EmailDeliveryWorker>();
        }

        services.AddScoped<IDomainEventHandler<ReviewCycleOpened>, CycleOpenedHandler>();
        services.AddScoped<IDomainEventHandler<EmployeeProfileUpdated>, ProfileUpdatedHandler>();

        services.AddScoped<NotificationApiService>();
        services.AddScoped<IValidator<TemplateRequest>, TemplateRequestValidator>();
        return services;
    }

    /// <summary>Applies the Notification migrations and adds the default templates that are missing.</summary>
    public static async Task InitializeNotificationAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await NotificationDefaults.EnsureAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>(), cancellationToken);
    }

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        NotificationEndpoints.Map(app);
        return app;
    }
}
