using System.Net.Http.Json;
using System.Text.Json;
using static Talent.Notification.IntegrationTests.NotificationHostFixture;

namespace Talent.Notification.IntegrationTests;

/// <summary>The whole chain with nobody calling the dispatcher: action → outbox → worker → alert and email.</summary>
public sealed class BackgroundWorkerTests(BackgroundNotificationHostFixture host) : IClassFixture<BackgroundNotificationHostFixture>
{
    [Fact]
    public async Task OpeningACycle_ReachesTheInboxAndTheMailbox_OnItsOwn()
    {
        using var admin = await host.LoginAsync(Admin);
        var name = $"Cycle {Guid.NewGuid():N}";
        var id = (await ReadAsync(await admin.PostAsJsonAsync("/api/v1/assessment/cycles",
            new { name, periodStart = "2026-01-01", periodEnd = "2026-12-31" }, Ct))).GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{id}/reviews",
            new { employeeId = host.Ids[Employee], reviewerId = host.Ids[Manager] }, Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/assessment/cycles/{id}/open", null, Ct)).EnsureSuccessStatusCode();
        using var manager = await host.LoginAsync(Manager);

        var deadline = DateTime.UtcNow.AddSeconds(30);
        var alerted = false;
        var mailed = false;
        while (DateTime.UtcNow < deadline && !(alerted && mailed))
        {
            var inbox = await manager.GetFromJsonAsync<JsonElement>("/api/v1/notifications/me", Ct);
            alerted = inbox.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("title").GetString() == $"{name} is open");
            mailed = host.Mail.Sent.Any(m => m.Subject == $"Review cycle {name} is open" && m.To == EmailOf(Manager));
            if (!(alerted && mailed))
            {
                await Task.Delay(250, Ct);
            }
        }

        alerted.ShouldBeTrue("the dispatcher worker should have delivered the event");
        mailed.ShouldBeTrue("the mail worker should have sent the email");
    }
}
