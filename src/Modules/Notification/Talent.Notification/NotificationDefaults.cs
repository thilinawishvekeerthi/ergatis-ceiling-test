using Microsoft.EntityFrameworkCore;
using Talent.Notification.Persistence;
using Talent.Notification.Templates;
using Talent.Shared.Security;

namespace Talent.Notification;

/// <summary>
/// The templates a new install starts with, so alerts work before HR writes its own words. Only missing templates are added:
/// a template HR changed or switched off is never overwritten.
/// </summary>
internal static class NotificationDefaults
{
    private static readonly (string Key, NotificationChannel Channel, string Subject, string Body)[] Templates =
    [
        (EventCatalog.CycleOpened, NotificationChannel.InApp, "{CycleName} is open", "The review cycle {CycleName} is open. Please rate the people assigned to you."),
        (EventCatalog.CycleOpened, NotificationChannel.Email, "Review cycle {CycleName} is open", "The review cycle {CycleName} is open.\n\nPlease sign in and rate the people assigned to you."),
        (EventCatalog.ProfileUpdated, NotificationChannel.InApp, "Your career profile was updated", "Your current job or competency levels changed. Open the Career Centre to see how close you are to your next job."),
        (EventCatalog.ProfileUpdated, NotificationChannel.Email, "Your career profile was updated", "Your current job or competency levels changed.\n\nSign in and open the Career Centre to see your next steps."),
    ];

    public static async Task EnsureAsync(NotificationDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var existing = (await db.Templates.Select(t => new { t.EventKey, t.Channel }).ToListAsync(cancellationToken))
            .Select(t => (t.EventKey, t.Channel)).ToHashSet();
        foreach (var (key, channel, subject, body) in Templates.Where(t => !existing.Contains((t.Key, t.Channel))))
        {
            db.Templates.Add(MessageTemplate.Create(key, channel, subject, body, isActive: true, SystemCurrentUser.Id, clock.GetUtcNow()));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
