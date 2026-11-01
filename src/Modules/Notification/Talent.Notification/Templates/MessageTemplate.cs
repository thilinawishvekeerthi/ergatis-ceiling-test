namespace Talent.Notification.Templates;

internal enum NotificationChannel
{
    InApp = 0,
    Email = 1,
}

/// <summary>The words of one alert on one channel. An alert type can have an in-app and an email template, each switched on or off.</summary>
internal sealed class MessageTemplate
{
    public const int MaxSubject = 200;
    public const int MaxBody = 4000;

    // For EF Core.
    private MessageTemplate()
    {
        EventKey = "";
        Subject = "";
        Body = "";
    }

    public Guid Id { get; private set; }
    public string EventKey { get; private set; }
    public NotificationChannel Channel { get; private set; }

    /// <summary>The email subject, or the title of the in-app alert.</summary>
    public string Subject { get; private set; }
    public string Body { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "";

    public static MessageTemplate Create(string eventKey, NotificationChannel channel, string subject, string body, bool isActive, string by, DateTimeOffset now)
    {
        var template = new MessageTemplate { Id = Guid.NewGuid(), EventKey = eventKey, Channel = channel };
        template.Set(subject, body, isActive, by, now);
        return template;
    }

    public void Set(string subject, string body, bool isActive, string by, DateTimeOffset now)
    {
        var definition = EventCatalog.Find(EventKey)
                         ?? throw new InvalidNotificationException($"'{EventKey}' is not a known alert type.");

        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > MaxSubject)
        {
            throw new InvalidNotificationException($"The subject must be 1 to {MaxSubject} characters.");
        }

        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > MaxBody)
        {
            throw new InvalidNotificationException($"The body must be 1 to {MaxBody} characters.");
        }

        var unknown = TemplateRenderer.PlaceholdersIn(subject).Concat(TemplateRenderer.PlaceholdersIn(body))
            .Where(p => !definition.Placeholders.Contains(p, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (unknown.Count > 0)
        {
            var allowed = definition.Placeholders.Count == 0 ? "none" : string.Join(", ", definition.Placeholders.Select(p => $"{{{p}}}"));
            throw new InvalidNotificationException(
                $"Unknown placeholder {string.Join(", ", unknown.Select(p => $"{{{p}}}"))} for {EventKey}. Allowed: {allowed}.");
        }

        Subject = subject.Trim();
        Body = body.Trim();
        IsActive = isActive;
        UpdatedBy = by;
        UpdatedAt = now;
    }
}
