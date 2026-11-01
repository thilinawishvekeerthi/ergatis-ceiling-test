using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Talent.Notification.IntegrationTests.NotificationHostFixture;

namespace Talent.Notification.IntegrationTests;

/// <summary>Message templates: HR writes the words, the system checks the placeholders, and changes show up in the next alert only.</summary>
public sealed class TemplateApiTests(NotificationHostFixture host) : IClassFixture<NotificationHostFixture>
{
    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, string key, string channel, object body) =>
        client.PutAsJsonAsync($"/api/v1/notifications/templates/{key}/{channel}", body, Ct);

    private async Task<string> OpenCycleForManagerAsync()
    {
        using var admin = await host.LoginAsync(Admin);
        var name = $"Cycle {Guid.NewGuid():N}";
        var id = (await OkAsync(await admin.PostAsJsonAsync("/api/v1/assessment/cycles",
            new { name, periodStart = "2026-01-01", periodEnd = "2026-12-31" }, Ct), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{id}/reviews",
            new { employeeId = host.Ids[Employee], reviewerId = host.Ids[Manager] }, Ct)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/assessment/cycles/{id}/open", null, Ct)).EnsureSuccessStatusCode();
        await host.DispatchEventsAsync();
        return name;
    }

    private async Task<List<string>> ManagerTitlesAsync()
    {
        using var manager = await host.LoginAsync(Manager);
        var inbox = await OkAsync(await manager.GetAsync("/api/v1/notifications/me?take=100", Ct));
        return [.. inbox.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!)];
    }

    [Fact]
    public async Task List_ShowsTheFourDefaultTemplates_WithTheirPlaceholders()
    {
        using var admin = await host.LoginAsync(Admin);

        var list = await OkAsync(await admin.GetAsync("/api/v1/notifications/templates", Ct));

        var rows = list.EnumerateArray().ToList();
        rows.Select(r => (r.GetProperty("eventKey").GetString(), r.GetProperty("channel").GetString()))
            .ShouldBe([("assessment.cycle-opened", "InApp"), ("assessment.cycle-opened", "Email"), ("career.profile-updated", "InApp"), ("career.profile-updated", "Email")]);
        rows.ShouldAllBe(r => r.GetProperty("isActive").GetBoolean());
        rows[0].GetProperty("placeholders").EnumerateArray().Select(p => p.GetString()).ShouldBe(["CycleName"]);
        rows[2].GetProperty("placeholders").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ChangedTemplate_IsUsedByTheNextAlert_AndOldAlertsKeepTheirText()
    {
        using var admin = await host.LoginAsync(Admin);
        var before = await OpenCycleForManagerAsync();
        try
        {
            (await OkAsync(await PutAsync(admin, "assessment.cycle-opened", "InApp",
                new { subject = "Time to rate: {CycleName}", body = "Open the app, {CycleName} needs you.", isActive = true }))).GetProperty("updatedBy").GetString().ShouldBe(Admin);

            var after = await OpenCycleForManagerAsync();

            var titles = await ManagerTitlesAsync();
            titles.ShouldContain($"{before} is open");
            titles.ShouldContain($"Time to rate: {after}");
            host.CountAudit("PutTemplate", Admin).ShouldBeGreaterThan(0);
        }
        finally
        {
            await PutAsync(admin, "assessment.cycle-opened", "InApp",
                new { subject = "{CycleName} is open", body = "The review cycle {CycleName} is open. Please rate the people assigned to you.", isActive = true });
        }
    }

    [Fact]
    public async Task SwitchingATemplateOff_StopsThatChannelOnly()
    {
        using var admin = await host.LoginAsync(Admin);
        try
        {
            (await PutAsync(admin, "assessment.cycle-opened", "InApp", new { subject = "{CycleName} is open", body = "Body {CycleName}", isActive = false })).EnsureSuccessStatusCode();

            var name = await OpenCycleForManagerAsync();

            (await ManagerTitlesAsync()).ShouldNotContain($"{name} is open");
            await host.SendMailAsync();
            host.Mail.Sent.Any(m => m.Subject.Contains(name, StringComparison.Ordinal)).ShouldBeTrue("the email template is still on");
        }
        finally
        {
            await PutAsync(admin, "assessment.cycle-opened", "InApp",
                new { subject = "{CycleName} is open", body = "The review cycle {CycleName} is open. Please rate the people assigned to you.", isActive = true });
        }
    }

    [Fact]
    public async Task Put_CreatesAMissingTemplate_ForAKnownAlertType()
    {
        using var admin = await host.LoginAsync(Admin);
        await using (var db = host.NewContext())
        {
            db.Templates.RemoveRange(db.Templates.Where(t => t.EventKey == "career.profile-updated" && t.Channel == Templates.NotificationChannel.Email));
            await db.SaveChangesAsync(Ct);
        }

        var created = await OkAsync(await PutAsync(admin, "career.profile-updated", "email", new { subject = "Profile changed", body = "Check the Career Centre.", isActive = true }));

        created.GetProperty("channel").GetString().ShouldBe("Email");
        created.GetProperty("subject").GetString().ShouldBe("Profile changed");
    }

    [Fact]
    public async Task Put_RejectsBadInput()
    {
        using var admin = await host.LoginAsync(Admin);

        var unknownPlaceholder = await PutAsync(admin, "assessment.cycle-opened", "InApp", new { subject = "Hi {FirstName}", body = "Body", isActive = true });
        (await OkAsync(unknownPlaceholder, HttpStatusCode.BadRequest)).GetProperty("detail").GetString()!.ShouldContain("{FirstName}");

        var empty = await OkAsync(await PutAsync(admin, "assessment.cycle-opened", "InApp", new { subject = "", body = "", isActive = (bool?)null }), HttpStatusCode.BadRequest);
        var errors = empty.GetProperty("errors");
        errors.GetProperty("subject").GetArrayLength().ShouldBeGreaterThan(0);
        errors.GetProperty("body").GetArrayLength().ShouldBeGreaterThan(0);
        errors.GetProperty("isActive").GetArrayLength().ShouldBeGreaterThan(0);

        (await PutAsync(admin, "assessment.cycle-opened", "Pigeon", new { subject = "S", body = "B", isActive = true })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PutAsync(admin, "no.such.event", "InApp", new { subject = "S", body = "B", isActive = true })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
