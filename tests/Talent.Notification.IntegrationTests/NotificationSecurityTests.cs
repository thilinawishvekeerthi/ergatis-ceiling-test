using System.Net;
using System.Net.Http.Json;
using static Talent.Notification.IntegrationTests.NotificationHostFixture;

namespace Talent.Notification.IntegrationTests;

/// <summary>Security tests for every Notification endpoint (talent-security skill): 401, 403 by role, and the anti-forgery check.</summary>
public sealed class NotificationSecurityTests(NotificationHostFixture host) : IClassFixture<NotificationHostFixture>
{
    public static TheoryData<string> Endpoints =>
    [
        "inbox", "unread-count", "read-one", "read-all", "list-templates", "put-template", "list-emails",
    ];

    private static Task<HttpResponseMessage> CallAsync(HttpClient client, string endpoint) => endpoint switch
    {
        "inbox" => client.GetAsync("/api/v1/notifications/me", Ct),
        "unread-count" => client.GetAsync("/api/v1/notifications/me/unread-count", Ct),
        "read-one" => client.PostAsync($"/api/v1/notifications/me/{Guid.NewGuid()}/read", null, Ct),
        "read-all" => client.PostAsync("/api/v1/notifications/me/read-all", null, Ct),
        "list-templates" => client.GetAsync("/api/v1/notifications/templates", Ct),
        "put-template" => client.PutAsJsonAsync("/api/v1/notifications/templates/career.profile-updated/InApp",
            new { subject = "Your career profile was updated", body = "Your current job or competency levels changed.", isActive = true }, Ct),
        "list-emails" => client.GetAsync("/api/v1/notifications/emails", Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutSession_Returns401(string endpoint)
    {
        using var client = host.Anonymous();

        var response = await CallAsync(client, endpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("list-templates")]
    [InlineData("put-template")]
    [InlineData("list-emails")]
    public async Task AdminEndpoints_AreForbiddenForEveryoneButHr(string endpoint)
    {
        foreach (var user in new[] { Employee, Manager, Committee })
        {
            using var client = await host.LoginAsync(user);
            (await CallAsync(client, endpoint)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{endpoint} as {user}");
        }

        using var admin = await host.LoginAsync(Admin);
        (await CallAsync(admin, endpoint)).StatusCode.ShouldBe(HttpStatusCode.OK, $"{endpoint} as {Admin}");
    }

    [Theory]
    [InlineData("inbox", Employee)]
    [InlineData("inbox", Manager)]
    [InlineData("inbox", Committee)]
    [InlineData("inbox", Admin)]
    [InlineData("unread-count", Employee)]
    [InlineData("read-all", Manager)]
    public async Task EveryRole_CanUseItsOwnInbox(string endpoint, string user)
    {
        using var client = await host.LoginAsync(user);

        (await CallAsync(client, endpoint)).StatusCode.ShouldBe(HttpStatusCode.OK, $"{endpoint} as {user}");
    }

    [Theory]
    [InlineData("read-one")]
    [InlineData("read-all")]
    [InlineData("put-template")]
    public async Task StateChangingCall_WithoutAntiforgeryHeader_Returns400(string endpoint)
    {
        using var client = await host.LoginAsync(Admin, withAntiforgery: false);

        (await CallAsync(client, endpoint)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
