using FluentValidation;
using Talent.Notification.Email;
using Talent.Notification.Inbox;
using Talent.Notification.Templates;
using Talent.Security.Contracts;

namespace Talent.Notification.Api;

internal sealed record NotificationResponse(
    [property: FieldClassification(FieldClass.Internal)] Guid Id,
    [property: FieldClassification(FieldClass.Internal)] string EventKey,
    [property: FieldClassification(FieldClass.Internal)] string Title,
    [property: FieldClassification(FieldClass.Internal)] string Body,
    [property: FieldClassification(FieldClass.Internal)] string? LinkUrl,
    [property: FieldClassification(FieldClass.Internal)] DateTimeOffset CreatedAt,
    [property: FieldClassification(FieldClass.Internal)] bool IsRead)
{
    public static NotificationResponse From(InAppNotification n) => new(n.Id, n.EventKey, n.Title, n.Body, n.LinkUrl, n.CreatedAt, n.IsRead);
}

internal sealed record InboxResponse(int UnreadCount, IReadOnlyList<NotificationResponse> Items);

internal sealed record UnreadCountResponse(int Count);

internal sealed record TemplateResponse(
    string EventKey, string Channel, string Description, IReadOnlyList<string> Placeholders,
    string Subject, string Body, bool IsActive, DateTimeOffset UpdatedAt, string UpdatedBy)
{
    public static TemplateResponse From(MessageTemplate t, EventDefinition definition) => new(
        t.EventKey, t.Channel.ToString(), definition.Description, definition.Placeholders, t.Subject, t.Body, t.IsActive, t.UpdatedAt, t.UpdatedBy);
}

/// <summary>What HR sees of a mail: who and when, never the text.</summary>
internal sealed record EmailResponse(
    Guid Id, Guid RecipientId, string EventKey, string Subject, string Status, int Attempts, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? SentAt)
{
    public static EmailResponse From(EmailMessage e) => new(
        e.Id, e.RecipientId, e.EventKey, e.Subject, e.Status.ToString(), e.Attempts, e.Error, e.CreatedAt, e.SentAt);
}

internal sealed record TemplateRequest(string? Subject, string? Body, bool? IsActive);

internal sealed class TemplateRequestValidator : AbstractValidator<TemplateRequest>
{
    public TemplateRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(MessageTemplate.MaxSubject);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(MessageTemplate.MaxBody);
        RuleFor(x => x.IsActive).NotNull().WithMessage("Say whether the template is active.");
    }
}
