namespace Talent.Notification.Inbox;

/// <summary>One alert in a person's inbox (the bell). Text is stored as rendered, so a later template change never rewrites history.</summary>
internal sealed class InAppNotification
{
    // For EF Core.
    private InAppNotification()
    {
        EventKey = "";
        Title = "";
        Body = "";
    }

    private InAppNotification(Guid recipientId, string eventKey, Guid sourceId, string title, string body, string? linkUrl, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        RecipientId = recipientId;
        EventKey = eventKey;
        SourceId = sourceId;
        Title = title;
        Body = body;
        LinkUrl = linkUrl;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public string EventKey { get; private set; }
    public Guid SourceId { get; private set; }
    public string Title { get; private set; }
    public string Body { get; private set; }
    public string? LinkUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static InAppNotification Create(Guid recipientId, string eventKey, Guid sourceId, string title, string body, string? linkUrl, DateTimeOffset now)
    {
        if (recipientId == Guid.Empty)
        {
            throw new InvalidNotificationException("An alert needs a recipient.");
        }

        if (linkUrl is not null && !IsLocalPath(linkUrl))
        {
            throw new InvalidNotificationException("An alert can only link to a page inside the app.");
        }

        return new InAppNotification(recipientId, eventKey, sourceId, title.Trim(), body.Trim(), linkUrl, now);
    }

    /// <summary>Marking an alert read twice keeps the first time.</summary>
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;

    /// <summary>Only a path on this site: starts with one slash. Anything else would be an open redirect from the bell.</summary>
    internal static bool IsLocalPath(string url) =>
        url.Length > 0 && url[0] == '/' && (url.Length == 1 || url[1] is not ('/' or '\\')) && !url.Contains('\n', StringComparison.Ordinal);
}
