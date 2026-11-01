using Talent.Assessment.Contracts;
using Talent.Career.Contracts;
using Talent.Notification.Contracts;
using Talent.Notification.Templates;
using Talent.Shared.Events;

namespace Talent.Notification.Handlers;

/// <summary>A review cycle opened: tell each reviewer they have people to rate.</summary>
internal sealed class CycleOpenedHandler(INotifier notifier) : IDomainEventHandler<ReviewCycleOpened>
{
    public Task HandleAsync(ReviewCycleOpened domainEvent, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(
            new NotifyRequest(
                EventCatalog.CycleOpened, domainEvent.EventId, domainEvent.ReviewerIds,
                new Dictionary<string, string> { ["CycleName"] = domainEvent.Name }),
            cancellationToken);
}

/// <summary>An employee's career profile changed: tell that employee.</summary>
internal sealed class ProfileUpdatedHandler(INotifier notifier) : IDomainEventHandler<EmployeeProfileUpdated>
{
    public Task HandleAsync(EmployeeProfileUpdated domainEvent, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(
            new NotifyRequest(EventCatalog.ProfileUpdated, domainEvent.EventId, [domainEvent.EmployeeId], new Dictionary<string, string>()),
            cancellationToken);
}
