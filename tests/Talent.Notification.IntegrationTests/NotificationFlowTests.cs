using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Talent.Notification.Contracts;
using static Talent.Notification.IntegrationTests.NotificationHostFixture;

namespace Talent.Notification.IntegrationTests;

/// <summary>Events in, alerts out: a real action in another module becomes an inbox alert and an email.</summary>
public sealed class NotificationFlowTests(NotificationHostFixture host) : IClassFixture<NotificationHostFixture>
{
    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await ReadAsync(response);
    }

    /// <summary>Creates a cycle where <paramref name="reviewer"/> rates the employee, and opens it. Returns the cycle name.</summary>
    private async Task<string> OpenCycleAsync(string reviewer)
    {
        using var admin = await host.LoginAsync(Admin);
        var name = $"Cycle {Guid.NewGuid():N}";
        var cycle = await OkAsync(await admin.PostAsJsonAsync("/api/v1/assessment/cycles",
            new { name, periodStart = "2026-01-01", periodEnd = "2026-12-31" }, Ct), HttpStatusCode.Created);
        var id = cycle.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{id}/reviews",
            new { employeeId = host.Ids[Employee], reviewerId = host.Ids[reviewer] }, Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/assessment/cycles/{id}/open", null, Ct)).EnsureSuccessStatusCode();
        return name;
    }

    private async Task<JsonElement> InboxAsync(string user, string query = "")
    {
        using var client = await host.LoginAsync(user);
        return await OkAsync(await client.GetAsync($"/api/v1/notifications/me{query}", Ct));
    }

    private static List<JsonElement> Items(JsonElement inbox, string containing) =>
        [.. inbox.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("title").GetString()!.Contains(containing, StringComparison.Ordinal))];

    [Fact]
    public async Task OpeningACycle_AlertsTheReviewer_AndNobodyElse()
    {
        var name = await OpenCycleAsync(Manager);
        await host.DispatchEventsAsync();

        var manager = Items(await InboxAsync(Manager), name);

        manager.Count.ShouldBe(1);
        manager[0].GetProperty("title").GetString().ShouldBe($"{name} is open");
        manager[0].GetProperty("body").GetString()!.ShouldContain(name);
        manager[0].GetProperty("linkUrl").GetString().ShouldBe("/manager");
        manager[0].GetProperty("isRead").GetBoolean().ShouldBeFalse();
        Items(await InboxAsync(Employee), name).ShouldBeEmpty();
        Items(await InboxAsync(OtherManager), name).ShouldBeEmpty();
        Items(await InboxAsync(Admin), name).ShouldBeEmpty();
    }

    [Fact]
    public async Task DeliveringTheSameEventTwice_StillGivesOneAlert()
    {
        var name = await OpenCycleAsync(Manager);
        await host.DispatchEventsAsync();
        // Pretend the outbox row was never marked done: the dispatcher will hand the event to the handlers again.
        using (var scope = host.Factory.Services.CreateScope())
        {
            var shared = scope.ServiceProvider.GetRequiredService<Talent.Shared.Persistence.TalentSharedDbContext>();
            foreach (var m in shared.OutboxMessages.Where(m => m.EventType.Contains("ReviewCycleOpened")))
            {
                m.ProcessedAt = null;
            }

            shared.ProcessedEvents.RemoveRange(shared.ProcessedEvents.Where(p => p.Handler.Contains("CycleOpenedHandler")));
            await shared.SaveChangesAsync(Ct);
        }

        await host.DispatchEventsAsync();

        Items(await InboxAsync(Manager), name).Count.ShouldBe(1);
    }

    [Fact]
    public async Task INotifier_WithTheSameSourceTwice_CreatesOneAlert()
    {
        using var scope = host.Factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();
        var request = new NotifyRequest("career.profile-updated", Guid.NewGuid(), [host.Ids[Admin]], new Dictionary<string, string>());

        await notifier.NotifyAsync(request, Ct);
        await notifier.NotifyAsync(request, Ct);

        (await InboxAsync(Admin)).GetProperty("items").EnumerateArray()
            .Count(i => i.GetProperty("title").GetString() == "Your career profile was updated").ShouldBe(1);
    }

    [Fact]
    public async Task INotifier_RejectsUnknownAlertTypesAndLinksOutsideTheApp()
    {
        using var scope = host.Factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<INotifier>();
        var values = new Dictionary<string, string>();

        await Should.ThrowAsync<Exception>(() => notifier.NotifyAsync(new NotifyRequest("no.such.key", Guid.NewGuid(), [host.Ids[Employee]], values), Ct));
        await Should.ThrowAsync<Exception>(() => notifier.NotifyAsync(
            new NotifyRequest("career.profile-updated", Guid.NewGuid(), [host.Ids[Employee]], values, "https://evil.example"), Ct));
    }

    [Fact]
    public async Task CareerProfileUpdate_AlertsThatEmployee()
    {
        using var admin = await host.LoginAsync(Admin);
        (await admin.PutAsJsonAsync($"/api/v1/career/employees/{host.Ids[Committee]}/profile",
            new { competencies = new[] { new { competencyCode = "SQL", level = 3 } } }, Ct)).EnsureSuccessStatusCode();
        await host.DispatchEventsAsync();

        var inbox = await InboxAsync(Committee);

        inbox.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("title").GetString() == "Your career profile was updated").ShouldBeTrue();
        inbox.GetProperty("unreadCount").GetInt32().ShouldBeGreaterThan(0);
    }

    // ---- reading and marking ----

    [Fact]
    public async Task UnreadCount_FallsAsAlertsAreRead_AndReadAllClearsIt()
    {
        var first = await OpenCycleAsync(OtherManager);
        var second = await OpenCycleAsync(OtherManager);
        await host.DispatchEventsAsync();
        using var client = await host.LoginAsync(OtherManager);

        (await OkAsync(await client.GetAsync("/api/v1/notifications/me/unread-count", Ct))).GetProperty("count").GetInt32().ShouldBeGreaterThanOrEqualTo(2);
        var inbox = await OkAsync(await client.GetAsync("/api/v1/notifications/me", Ct));
        var before = inbox.GetProperty("unreadCount").GetInt32();
        var target = Items(inbox, first).Single().GetProperty("id").GetGuid();

        var read = await OkAsync(await client.PostAsync($"/api/v1/notifications/me/{target}/read", null, Ct));
        read.GetProperty("isRead").GetBoolean().ShouldBeTrue();
        (await OkAsync(await client.GetAsync("/api/v1/notifications/me/unread-count", Ct))).GetProperty("count").GetInt32().ShouldBe(before - 1);

        var unreadOnly = await OkAsync(await client.GetAsync("/api/v1/notifications/me?unreadOnly=true", Ct));
        Items(unreadOnly, first).ShouldBeEmpty();
        Items(unreadOnly, second).Count.ShouldBe(1);

        (await OkAsync(await client.PostAsync("/api/v1/notifications/me/read-all", null, Ct))).GetProperty("count").GetInt32().ShouldBe(0);
        (await OkAsync(await client.GetAsync("/api/v1/notifications/me/unread-count", Ct))).GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Inbox_NeverShowsOrChangesAnotherPersonsAlert()
    {
        var name = await OpenCycleAsync(Manager);
        await host.DispatchEventsAsync();
        var managersAlert = Items(await InboxAsync(Manager), name).Single().GetProperty("id").GetGuid();
        using var other = await host.LoginAsync(OtherManager);

        (await other.PostAsync($"/api/v1/notifications/me/{managersAlert}/read", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/v1/notifications/me/{Guid.NewGuid()}/read", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        Items(await InboxAsync(Manager), name).Single().GetProperty("isRead").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Inbox_TakeIsCapped_AndNewestComeFirst()
    {
        await OpenCycleAsync(Manager);
        await OpenCycleAsync(Manager);
        await host.DispatchEventsAsync();

        var one = await InboxAsync(Manager, "?take=1");
        var many = await InboxAsync(Manager, "?take=100000");

        one.GetProperty("items").GetArrayLength().ShouldBe(1);
        many.GetProperty("items").GetArrayLength().ShouldBeLessThanOrEqualTo(100);
        var times = many.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("createdAt").GetDateTimeOffset()).ToList();
        times.ShouldBe([.. times.OrderByDescending(t => t)]);
    }

    // ---- email ----

    [Fact]
    public async Task OpeningACycle_AlsoQueuesAnEmail_ThatTheDeliveryServiceSends()
    {
        var name = await OpenCycleAsync(Manager);
        await host.DispatchEventsAsync();

        (await host.SendMailAsync()).ShouldBeGreaterThan(0);

        var mail = host.Mail.Sent.Single(m => m.Subject.Contains(name, StringComparison.Ordinal));
        mail.To.ShouldBe(EmailOf(Manager));
        mail.Subject.ShouldBe($"Review cycle {name} is open");
        mail.Body.ShouldContain(name);
        mail.Body.ShouldNotContain("{");
        host.Mail.Sent.Any(m => m.Subject.Contains(name, StringComparison.Ordinal) && m.To != EmailOf(Manager)).ShouldBeFalse();
    }

    [Fact]
    public async Task EmployeeWithNoEmailAddress_GetsTheAlertButTheEmailIsSkipped()
    {
        var name = await OpenCycleAsync(NoMail);
        await host.DispatchEventsAsync();
        await host.SendMailAsync();

        Items(await InboxAsync(NoMail), name).Count.ShouldBe(1);
        host.Mail.Sent.Any(m => m.Subject.Contains(name, StringComparison.Ordinal)).ShouldBeFalse();
        using var admin = await host.LoginAsync(Admin);
        var skipped = await OkAsync(await admin.GetAsync("/api/v1/notifications/emails?status=Skipped", Ct));
        skipped.EnumerateArray().Any(e => e.GetProperty("subject").GetString()!.Contains(name, StringComparison.Ordinal)
                                          && e.GetProperty("recipientId").GetGuid() == host.Ids[NoMail]).ShouldBeTrue();
    }

    [Fact]
    public async Task AdminDeliveryList_FiltersByStatus_AndRejectsBadOnes()
    {
        var name = await OpenCycleAsync(Manager);
        await host.DispatchEventsAsync();
        using var admin = await host.LoginAsync(Admin);

        var pending = await OkAsync(await admin.GetAsync("/api/v1/notifications/emails?status=pending", Ct));
        pending.EnumerateArray().Any(e => e.GetProperty("subject").GetString()!.Contains(name, StringComparison.Ordinal)).ShouldBeTrue();
        pending.EnumerateArray().ShouldAllBe(e => e.GetProperty("status").GetString() == "Pending");
        foreach (var row in pending.EnumerateArray())
        {
            row.TryGetProperty("body", out _).ShouldBeFalse("HR sees who and when, never the text");
        }

        (await admin.GetAsync("/api/v1/notifications/emails?status=Bogus", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
