namespace Talent.Notification.Contracts;

/// <summary>
/// One alert to send. <see cref="SourceId"/> makes the call idempotent: sending the same source to the same recipient
/// with the same <see cref="EventKey"/> twice creates one alert. Use the id of the event or job that caused it.
/// </summary>
/// <param name="EventKey">A key from the event catalogue, for example <c>assessment.cycle-opened</c>. It picks the message templates.</param>
/// <param name="Values">Fills the {Placeholders} of the templates.</param>
/// <param name="LinkUrl">A path inside the app the alert opens, such as <c>/manager</c>. Never an outside address.</param>
public sealed record NotifyRequest(
    string EventKey,
    Guid SourceId,
    IReadOnlyCollection<Guid> RecipientIds,
    IReadOnlyDictionary<string, string> Values,
    string? LinkUrl = null);

/// <summary>Plug point for other modules and jobs that want to alert people without raising a domain event.</summary>
public interface INotifier
{
    Task NotifyAsync(NotifyRequest request, CancellationToken cancellationToken);
}
