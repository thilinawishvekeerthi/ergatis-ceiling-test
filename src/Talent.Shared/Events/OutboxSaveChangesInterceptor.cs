using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Talent.Shared.Persistence;

namespace Talent.Shared.Events;

/// <summary>
/// Copies the domain events of saved entities into <c>shared.outbox_messages</c> in the same transaction (ADR-10).
/// The context must map the shared tables (<see cref="SharedModel.MapSharedTables"/>).
/// </summary>
public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Collect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var sources = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        foreach (var source in sources)
        {
            foreach (var domainEvent in source.DomainEvents)
            {
                var type = domainEvent.GetType();
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = domainEvent.EventId,
                    EventType = type.AssemblyQualifiedName!,
                    Payload = JsonSerializer.Serialize(domainEvent, type),
                    OccurredAt = domainEvent.OccurredAt,
                });
            }

            source.ClearDomainEvents();
        }
    }
}
