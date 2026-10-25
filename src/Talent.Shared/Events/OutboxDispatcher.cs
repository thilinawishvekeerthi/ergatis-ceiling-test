using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Talent.Shared.Persistence;

namespace Talent.Shared.Events;

/// <summary>
/// Delivers pending outbox messages to in-process handlers after commit (ADR-10).
/// Handlers are idempotent: each (handler, event) pair is recorded, so a second delivery does nothing.
/// </summary>
public sealed partial class OutboxDispatcher(
    TalentSharedDbContext db,
    IServiceProvider services,
    TimeProvider clock,
    ILogger<OutboxDispatcher> logger)
{
    private const int MaxErrorLength = 1000;

    /// <summary>Processes up to <paramref name="batchSize"/> pending messages. Returns how many finished.</summary>
    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var done = 0;
        foreach (var message in pending)
        {
            message.Attempts++;
            try
            {
                await DeliverAsync(message, cancellationToken);
                message.ProcessedAt = clock.GetUtcNow();
                message.Error = null;
                done++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDeliveryFailed(logger, message.Id, ex.GetType().Name);
                message.Error = Truncate($"{ex.GetType().Name}: {ex.Message}");
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return done;
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var type = Type.GetType(message.EventType, throwOnError: true)!;
        if (!typeof(IDomainEvent).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"{type.Name} is not a domain event.");
        }

        var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(message.Payload, type)!;
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(type);
        var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        foreach (var handler in services.GetServices(handlerType))
        {
            var handlerName = handler!.GetType().FullName!;
            var already = await db.ProcessedEvents.AnyAsync(
                p => p.Handler == handlerName && p.EventId == message.Id, cancellationToken);
            if (already)
            {
                continue;
            }

            await InvokeAsync(handle, handler, domainEvent, cancellationToken);

            db.ProcessedEvents.Add(new ProcessedEvent
            {
                Handler = handlerName,
                EventId = message.Id,
                ProcessedAt = clock.GetUtcNow(),
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task InvokeAsync(
        System.Reflection.MethodInfo handle, object handler, IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            await (Task)handle.Invoke(handler, [domainEvent, cancellationToken])!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxErrorLength ? value : value[..MaxErrorLength];

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox message {MessageId} failed with {ExceptionType}")]
    private static partial void LogDeliveryFailed(ILogger logger, Guid messageId, string exceptionType);
}
