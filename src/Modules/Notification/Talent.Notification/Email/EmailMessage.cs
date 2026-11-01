namespace Talent.Notification.Email;

internal enum EmailStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,

    /// <summary>Nothing to send to: the employee has no email address on record.</summary>
    Skipped = 3,
}

/// <summary>An email waiting to go out, or already sent. Sending is a separate step from creating it, so a slow mail server never blocks an event.</summary>
internal sealed class EmailMessage
{
    public const int MaxAttempts = 5;

    // For EF Core.
    private EmailMessage()
    {
        EventKey = "";
        Subject = "";
        Body = "";
    }

    private EmailMessage(Guid recipientId, string? toAddress, string eventKey, Guid sourceId, string subject, string body, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        RecipientId = recipientId;
        ToAddress = toAddress;
        EventKey = eventKey;
        SourceId = sourceId;
        Subject = subject;
        Body = body;
        CreatedAt = now;
        Status = string.IsNullOrWhiteSpace(toAddress) ? EmailStatus.Skipped : EmailStatus.Pending;
        Error = Status == EmailStatus.Skipped ? "No email address on record." : null;
    }

    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public string? ToAddress { get; private set; }
    public string EventKey { get; private set; }
    public Guid SourceId { get; private set; }
    public string Subject { get; private set; }
    public string Body { get; private set; }
    public EmailStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }

    public static EmailMessage Create(Guid recipientId, string? toAddress, string eventKey, Guid sourceId, string subject, string body, DateTimeOffset now)
    {
        if (recipientId == Guid.Empty)
        {
            throw new InvalidNotificationException("An email needs a recipient.");
        }

        return new EmailMessage(recipientId, toAddress?.Trim(), eventKey, sourceId, subject.Trim(), body.Trim(), now);
    }

    public void MarkSent(DateTimeOffset now)
    {
        RequirePending();
        Attempts++;
        LastAttemptAt = now;
        Status = EmailStatus.Sent;
        SentAt = now;
        Error = null;
    }

    /// <summary>The message stays Pending (so it is tried again) until it has failed <see cref="MaxAttempts"/> times, then it is Failed for good.</summary>
    public void MarkAttemptFailed(string error, DateTimeOffset now)
    {
        RequirePending();
        Attempts++;
        LastAttemptAt = now;
        Error = error.Length <= 500 ? error : error[..500];
        if (Attempts >= MaxAttempts)
        {
            Status = EmailStatus.Failed;
        }
    }

    /// <summary>A failed email waits longer after each failure: 1 minute after the first, 2 after the second, and so on.</summary>
    public bool IsDue(DateTimeOffset now) =>
        Status == EmailStatus.Pending && (LastAttemptAt is null || now - LastAttemptAt >= TimeSpan.FromMinutes(Attempts));

    private void RequirePending()
    {
        if (Status != EmailStatus.Pending)
        {
            throw new InvalidNotificationException($"This email is {Status} and cannot be sent.");
        }
    }
}
