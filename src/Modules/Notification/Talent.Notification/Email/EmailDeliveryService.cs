using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Talent.Notification.Persistence;

namespace Talent.Notification.Email;

/// <summary>Sends the emails that are waiting. One failure never stops the others: it is recorded and tried again later.</summary>
internal sealed class EmailDeliveryService(NotificationDbContext db, IEmailSender sender, TimeProvider clock)
{
    /// <summary>Sends up to <paramref name="batchSize"/> due emails. Returns how many were sent.</summary>
    public async Task<int> SendPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var now = clock.GetUtcNow();
        var waiting = await db.Emails.Where(e => e.Status == EmailStatus.Pending).OrderBy(e => e.CreatedAt).Take(batchSize * 5).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var mail in waiting.Where(m => m.IsDue(now)).Take(batchSize))
        {
            try
            {
                await sender.SendAsync(mail.ToAddress!, mail.Subject, mail.Body, cancellationToken);
                mail.MarkSent(clock.GetUtcNow());
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mail.MarkAttemptFailed($"{ex.GetType().Name}: {ex.Message}", clock.GetUtcNow());
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }
}

/// <summary>Checks the mail queue on a timer. It is registered only when <c>Notification:Email:PollSeconds</c> is above 0.</summary>
internal sealed partial class EmailDeliveryWorker(
    IServiceScopeFactory scopes, IOptions<EmailOptions> options, TimeProvider clock, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<EmailDeliveryService>().SendPendingAsync(options.Value.BatchSize, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogPassFailed(logger, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is stopping.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email delivery pass failed with {ExceptionType}")]
    private static partial void LogPassFailed(ILogger logger, string exceptionType);
}
