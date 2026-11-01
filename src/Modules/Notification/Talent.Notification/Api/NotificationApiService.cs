using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Talent.Notification.Email;
using Talent.Notification.Persistence;
using Talent.Notification.Templates;
using Talent.Security.Contracts;
using Talent.Shared.Auditing;
using Talent.Shared.Errors;
using Talent.Shared.Security;

namespace Talent.Notification.Api;

/// <summary>
/// The Notification API rules (ADR-07). Your inbox is only ever your own: the permission matrix lets a role use it (403),
/// and every query is filtered to the signed-in employee, so no id can reach someone else's alert.
/// Resources: <c>notification:Inbox</c>, <c>notification:Template</c> (View, Edit) and <c>notification:Delivery</c> (View).
/// </summary>
internal sealed class NotificationApiService(
    NotificationDbContext db,
    IAccessGuard guard,
    IFieldMasker masker,
    ISecurityUser user,
    ICurrentUser audit,
    TimeProvider clock,
    IValidator<TemplateRequest> templateValidator)
{
    public const string InboxResource = "notification:Inbox";
    public const string TemplateResource = "notification:Template";
    public const string DeliveryResource = "notification:Delivery";
    public const int MaxTake = 100;

    public async Task<InboxResponse> GetInboxAsync(bool unreadOnly, int? take, CancellationToken cancellationToken)
    {
        var (me, grant) = await RequireInboxAsync(cancellationToken);
        var limit = Math.Clamp(take ?? 20, 1, MaxTake);
        var mine = db.Notifications.AsNoTracking().Where(n => n.RecipientId == me);
        var items = await mine.Where(n => !unreadOnly || n.ReadAt == null)
            .OrderByDescending(n => n.CreatedAt).Take(limit)
            .ToListAsync(cancellationToken);
        var unread = await mine.CountAsync(n => n.ReadAt == null, cancellationToken);
        return new InboxResponse(unread, [.. items.Select(n => masker.Mask(NotificationResponse.From(n), grant, isOwnRecord: true))]);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        var (me, _) = await RequireInboxAsync(cancellationToken);
        return new UnreadCountResponse(await db.Notifications.CountAsync(n => n.RecipientId == me && n.ReadAt == null, cancellationToken));
    }

    /// <summary>Marking your own alert read is part of viewing your inbox, so it needs View and not Edit (Edit on your own record is blocked by the conflict-of-interest rule).</summary>
    public async Task<NotificationResponse> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        var (me, grant) = await RequireInboxAsync(cancellationToken);
        var alert = await db.Notifications.SingleOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == me, cancellationToken)
                    ?? throw new NotificationNotFoundException("Notification", notificationId.ToString());
        alert.MarkRead(clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return masker.Mask(NotificationResponse.From(alert), grant, isOwnRecord: true);
    }

    public async Task<UnreadCountResponse> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var (me, _) = await RequireInboxAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var unread = await db.Notifications.Where(n => n.RecipientId == me && n.ReadAt == null).ToListAsync(cancellationToken);
        foreach (var alert in unread)
        {
            alert.MarkRead(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new UnreadCountResponse(0);
    }

    public async Task<IReadOnlyList<TemplateResponse>> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        await guard.RequireAsync(TemplateResource, PermissionAction.View, null, cancellationToken);
        var templates = await db.Templates.AsNoTracking().ToListAsync(cancellationToken);
        return [.. templates
            .Select(t => (Template: t, Definition: EventCatalog.Find(t.EventKey)))
            .Where(x => x.Definition is not null)
            .OrderBy(x => x.Template.EventKey, StringComparer.Ordinal).ThenBy(x => x.Template.Channel)
            .Select(x => TemplateResponse.From(x.Template, x.Definition!))];
    }

    /// <summary>Creates the template for this alert type and channel, or changes it. The text is checked against the placeholders of the alert type.</summary>
    public async Task<TemplateResponse> PutTemplateAsync(string eventKey, string channel, TemplateRequest request, CancellationToken cancellationToken)
    {
        Validate(templateValidator, request);
        await guard.RequireAsync(TemplateResource, PermissionAction.Edit, null, cancellationToken);
        var definition = EventCatalog.Find(eventKey) ?? throw new NotificationNotFoundException("Alert type", eventKey);
        if (!Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["channel"] = ["The channel must be InApp or Email."] });
        }

        var template = await db.Templates.SingleOrDefaultAsync(t => t.EventKey == eventKey && t.Channel == parsed, cancellationToken);
        var now = clock.GetUtcNow();
        if (template is null)
        {
            template = MessageTemplate.Create(eventKey, parsed, request.Subject!, request.Body!, request.IsActive!.Value, audit.UserId, now);
            db.Templates.Add(template);
        }
        else
        {
            template.Set(request.Subject!, request.Body!, request.IsActive!.Value, audit.UserId, now);
        }

        db.AddAudit(audit, clock, "PutTemplate", nameof(MessageTemplate), $"{eventKey}/{parsed}",
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { template.IsActive }));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new NotificationConflictException("Someone else changed this template at the same time. Reload and try again.");
        }

        return TemplateResponse.From(template, definition);
    }

    public async Task<IReadOnlyList<EmailResponse>> ListEmailsAsync(string? status, int? take, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(DeliveryResource, PermissionAction.View, null, cancellationToken);
        EmailStatus? wanted = null;
        if (!string.IsNullOrEmpty(status))
        {
            wanted = Enum.TryParse<EmailStatus>(status, ignoreCase: true, out var s) && Enum.IsDefined(s)
                ? s
                : throw new ValidationFailedException(new Dictionary<string, string[]> { ["status"] = ["The status must be Pending, Sent, Failed or Skipped."] });
        }

        var rows = await db.Emails.AsNoTracking()
            .Where(e => wanted == null || e.Status == wanted)
            .OrderByDescending(e => e.CreatedAt).Take(Math.Clamp(take ?? 50, 1, MaxTake))
            .ToListAsync(cancellationToken);
        return [.. rows.Select(EmailResponse.From)];
    }

    private async Task<(Guid Me, AccessGrant Grant)> RequireInboxAsync(CancellationToken cancellationToken)
    {
        var grant = await guard.RequireGrantAsync(InboxResource, PermissionAction.View, cancellationToken);
        return (user.EmployeeId ?? throw new ForbiddenException("Your login is not linked to an employee."), grant);
    }

    private static void Validate<T>(IValidator<T> validator, T value)
    {
        var result = validator.Validate(value);
        if (!result.IsValid)
        {
            throw new ValidationFailedException(result.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? e.PropertyName : char.ToLowerInvariant(e.PropertyName[0]) + e.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
    }
}
