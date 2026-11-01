using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talent.Integration.Contracts;
using Talent.Notification.Contracts;
using Talent.Notification.Email;
using Talent.Notification.Inbox;
using Talent.Notification.Persistence;
using Talent.Notification.Templates;

namespace Talent.Notification;

/// <summary>
/// Turns one alert into the in-app notifications and email messages of its recipients, using the active templates (ADR-10).
/// It is idempotent: the same source, recipient and event key creates nothing the second time.
/// </summary>
internal sealed class NotificationEngine(NotificationDbContext db, IEmployeeReader employees, TimeProvider clock) : INotifier
{
    public async Task NotifyAsync(NotifyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var definition = EventCatalog.Find(request.EventKey)
                         ?? throw new InvalidNotificationException($"'{request.EventKey}' is not a known alert type.");
        var link = request.LinkUrl ?? definition.DefaultLink;
        if (link is not null && !InAppNotification.IsLocalPath(link))
        {
            throw new InvalidNotificationException("An alert can only link to a page inside the app.");
        }

        var recipients = request.RecipientIds.Where(r => r != Guid.Empty).Distinct().ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        var templates = await db.Templates.AsNoTracking()
            .Where(t => t.EventKey == request.EventKey && t.IsActive)
            .ToListAsync(cancellationToken);
        var inApp = templates.FirstOrDefault(t => t.Channel == NotificationChannel.InApp);
        var email = templates.FirstOrDefault(t => t.Channel == NotificationChannel.Email);
        if (inApp is null && email is null)
        {
            return;
        }

        foreach (var recipient in recipients)
        {
            await CreateForAsync(request, recipient, link, inApp, email, cancellationToken);
        }
    }

    private async Task CreateForAsync(
        NotifyRequest request, Guid recipient, string? link, MessageTemplate? inApp, MessageTemplate? email, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (inApp is not null && !await db.Notifications.AnyAsync(n => n.SourceId == request.SourceId && n.RecipientId == recipient && n.EventKey == request.EventKey, cancellationToken))
        {
            db.Notifications.Add(InAppNotification.Create(
                recipient, request.EventKey, request.SourceId,
                TemplateRenderer.Render(inApp.Subject, request.Values), TemplateRenderer.Render(inApp.Body, request.Values), link, now));
        }

        if (email is not null && !await db.Emails.AnyAsync(e => e.SourceId == request.SourceId && e.RecipientId == recipient && e.EventKey == request.EventKey, cancellationToken))
        {
            var address = (await employees.GetAsync(recipient, cancellationToken))?.Email;
            db.Emails.Add(EmailMessage.Create(
                recipient, address, request.EventKey, request.SourceId,
                TemplateRenderer.Render(email.Subject, request.Values), TemplateRenderer.Render(email.Body, request.Values), now));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another worker created the same alert between our check and our save. That is the outcome we wanted.
            db.ChangeTracker.Clear();
        }
    }
}
