namespace Talent.Notification.Api;

/// <summary>
/// <c>/api/v1/notifications</c>. The group needs a signed-in user (401). Each call then checks the permission matrix
/// in <see cref="NotificationApiService"/> (403). State-changing calls also need the anti-forgery header (400), checked in the host pipeline.
/// </summary>
internal static class NotificationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications").RequireAuthorization();

        group.MapGet("/me", async (bool? unreadOnly, int? take, NotificationApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetInboxAsync(unreadOnly ?? false, take, ct)));

        group.MapGet("/me/unread-count", async (NotificationApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetUnreadCountAsync(ct)));

        group.MapPost("/me/read-all", async (NotificationApiService api, CancellationToken ct) =>
            Results.Ok(await api.MarkAllReadAsync(ct)));

        group.MapPost("/me/{notificationId:guid}/read", async (Guid notificationId, NotificationApiService api, CancellationToken ct) =>
            Results.Ok(await api.MarkReadAsync(notificationId, ct)));

        group.MapGet("/templates", async (NotificationApiService api, CancellationToken ct) =>
            Results.Ok(await api.ListTemplatesAsync(ct)));

        group.MapPut("/templates/{eventKey}/{channel}", async (string eventKey, string channel, TemplateRequest request, NotificationApiService api, CancellationToken ct) =>
            Results.Ok(await api.PutTemplateAsync(eventKey, channel, request, ct)));

        group.MapGet("/emails", async (string? status, int? take, NotificationApiService api, CancellationToken ct) =>
            Results.Ok(await api.ListEmailsAsync(status, take, ct)));
    }
}
