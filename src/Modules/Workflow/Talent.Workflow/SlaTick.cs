using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Talent.Workflow.Contracts;
using Talent.Workflow.Persistence;

namespace Talent.Workflow;

/// <summary>One pass over overdue approval tasks. Each task runs in its own scope, so one failure does not block the others.</summary>
internal sealed partial class SlaTickRunner(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SlaTickRunner> logger)
{
    private const int BatchSize = 100;

    /// <summary>Returns how many tasks were escalated, expired or marked breached.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        List<Guid> due;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
            var now = clock.GetUtcNow();
            due = await db.Tasks.AsNoTracking()
                .Where(t => t.Status == ApprovalTaskStatus.Pending && t.SlaBreachedAt == null && t.DueAt != null && t.DueAt <= now)
                .OrderBy(t => t.DueAt).ThenBy(t => t.Id)
                .Select(t => t.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }

        var handled = 0;
        foreach (var taskId in due)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (await scope.ServiceProvider.GetRequiredService<SlaProcessor>().ProcessAsync(taskId, cancellationToken))
                {
                    handled++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogTaskFailed(logger, taskId, ex.GetType().Name);
            }
        }

        return handled;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SLA processing failed for approval task {TaskId} with {ExceptionType}")]
    private static partial void LogTaskFailed(ILogger logger, Guid taskId, string exceptionType);
}

/// <summary>Runs the SLA pass on a timer (ADR-05). The first pass waits one interval.</summary>
internal sealed partial class SlaTickService(
    SlaTickRunner runner, IOptions<WorkflowOptions> options, TimeProvider clock, ILogger<SlaTickService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SlaTickSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await runner.RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogTickFailed(logger, ex.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is stopping.
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SLA tick failed with {ExceptionType}")]
    private static partial void LogTickFailed(ILogger logger, string exceptionType);
}
