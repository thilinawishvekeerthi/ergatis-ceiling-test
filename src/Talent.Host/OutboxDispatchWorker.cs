using Talent.Shared.Events;

namespace Talent.Host;

/// <summary>
/// Delivers pending outbox messages to their handlers on a timer (ADR-10), so events such as "cycle opened" reach Notification
/// without anyone calling the dispatcher. Registered only when <c>Outbox:DispatchIntervalSeconds</c> is above 0.
/// </summary>
internal sealed partial class OutboxDispatchWorker(IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider clock, ILogger<OutboxDispatchWorker> logger)
    : BackgroundService
{
    public const string IntervalSetting = "Outbox:DispatchIntervalSeconds";
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configuration.GetValue<int>(IntervalSetting)), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                    while (await dispatcher.DispatchPendingAsync(BatchSize, stoppingToken) == BatchSize)
                    {
                    }
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox dispatch pass failed with {ExceptionType}")]
    private static partial void LogPassFailed(ILogger logger, string exceptionType);
}
