using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Talent.Host.Tests;

/// <summary>
/// The whole stack through HTTP, as the React app calls it: BFF login, Config admin, and an approval routed with synced HR data.
/// Users come from appsettings.Development.json: dev.employee (E00002) reports to dev.manager (E00001), dev.admin is HR.
/// </summary>
public sealed class EndToEndFlowTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HrAdmin_EditsAndPublishesANewVersionOfTheNineBox()
    {
        using var admin = await LoginAsync("dev.admin");

        var grids = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/config/grid", UriKind.Relative), Ct));
        var nineBox = grids.EnumerateArray().Single(g => g.GetProperty("code").GetString() == "NINE_BOX");
        var published = await ReadAsync(await admin.GetAsync(new Uri($"/api/v1/config/versions/{nineBox.GetProperty("latestVersionId")}", UriKind.Relative), Ct));
        published.GetProperty("status").GetString().ShouldBe("Published");

        var draftResponse = await admin.PostAsync(new Uri("/api/v1/config/grid/NINE_BOX/versions", UriKind.Relative), null, Ct);
        draftResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var draft = await ReadAsync(draftResponse);
        draft.GetProperty("status").GetString().ShouldBe("Draft");

        var payload = published.GetProperty("payload").GetRawText().Replace("\"Star\"", "\"Future leader\"", StringComparison.Ordinal);
        var saved = await admin.PutAsJsonAsync(
            new Uri($"/api/v1/config/versions/{draft.GetProperty("id")}/payload", UriKind.Relative),
            new { payload = JsonSerializer.Deserialize<JsonElement>(payload) }, Ct);
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);

        var publish = await admin.PostAsync(new Uri($"/api/v1/config/versions/{draft.GetProperty("id")}/publish", UriKind.Relative), null, Ct);
        publish.StatusCode.ShouldBe(HttpStatusCode.OK);

        var latest = await ReadAsync(await admin.GetAsync(new Uri("/api/v1/config/grid/NINE_BOX/latest", UriKind.Relative), Ct));
        latest.GetProperty("versionNo").GetInt32().ShouldBe(published.GetProperty("versionNo").GetInt32() + 1);
        latest.GetProperty("payload").GetRawText().ShouldContain("Future leader");
    }

    [Fact]
    public async Task Employee_ListOfConfig_IsForbiddenForGrids()
    {
        using var employee = await LoginAsync("dev.employee");

        (await employee.GetAsync(new Uri("/api/v1/config/grid", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DevelopmentPlan_GoesToTheSyncedManager_ThenHrWhenCostly_ThenIsApproved()
    {
        using var employee = await LoginAsync("dev.employee");
        using var manager = await LoginAsync("dev.manager");
        using var admin = await LoginAsync("dev.admin");

        var started = await employee.PostAsJsonAsync(new Uri("/api/v1/workflows", UriKind.Relative), new
        {
            definitionCode = "IDP_APPROVAL",
            subjectType = "Idp",
            context = new Dictionary<string, string> { ["title"] = "Leadership course", ["estimatedCost"] = "1500" },
        }, Ct);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await ReadAsync(started)).GetProperty("id").GetString();

        var submitted = await ActAsync(employee, id, "Submit");
        submitted.GetProperty("currentState").GetString().ShouldBe("PendingManager");
        submitted.GetProperty("openTask").GetProperty("assignees").EnumerateArray().Select(a => a.GetString()).ShouldBe(["dev.manager"]);

        // The manager finds it in their inbox. Nobody else can see it.
        var inbox = await ReadAsync(await manager.GetAsync(new Uri("/api/v1/workflows/inbox", UriKind.Relative), Ct));
        var item = inbox.EnumerateArray().Single(i => i.GetProperty("task").GetProperty("instanceId").GetString() == id);
        item.GetProperty("context").GetProperty("title").GetString().ShouldBe("Leadership course");
        (await admin.GetAsync(new Uri($"/api/v1/workflows/{id}", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await employee.PostAsJsonAsync(new Uri($"/api/v1/workflows/{id}/actions", UriKind.Relative), new { action = "Approve" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 1500 is over the 1000 limit, so HR approves as well.
        (await ActAsync(manager, id, "Approve")).GetProperty("currentState").GetString().ShouldBe("PendingHr");
        var approved = await ActAsync(admin, id, "Approve");

        approved.GetProperty("currentState").GetString().ShouldBe("Approved");
        approved.GetProperty("status").GetString().ShouldBe("Completed");
        approved.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("actor").GetString())
            .ShouldBe(["dev.employee", "dev.employee", "dev.manager", "dev.admin"]);
        var mine = await ReadAsync(await employee.GetAsync(new Uri("/api/v1/workflows/mine", UriKind.Relative), Ct));
        mine.EnumerateArray().Single(w => w.GetProperty("id").GetString() == id).GetProperty("currentState").GetString().ShouldBe("Approved");
    }

    [Fact]
    public async Task Reject_WithoutComment_Returns400_AndWithComment_ReturnsThePlanToTheEmployee()
    {
        using var employee = await LoginAsync("dev.employee");
        using var manager = await LoginAsync("dev.manager");
        var started = await employee.PostAsJsonAsync(new Uri("/api/v1/workflows", UriKind.Relative), new
        {
            definitionCode = "IDP_APPROVAL",
            subjectType = "Idp",
            context = new Dictionary<string, string> { ["title"] = "Conference", ["estimatedCost"] = "200" },
        }, Ct);
        var id = (await ReadAsync(started)).GetProperty("id").GetString();
        await ActAsync(employee, id, "Submit");

        (await manager.PostAsJsonAsync(new Uri($"/api/v1/workflows/{id}/actions", UriKind.Relative), new { action = "Reject" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var rejected = await ActAsync(manager, id, "Reject", "Pick a cheaper one");

        rejected.GetProperty("currentState").GetString().ShouldBe("Draft");
        rejected.GetProperty("history").EnumerateArray().Last().GetProperty("comment").GetString().ShouldBe("Pick a cheaper one");
    }

    [Fact]
    public async Task StartWorkflow_WithoutSession_Returns401_AndWithoutAntiforgery_Returns400()
    {
        using var anonymous = host.Factory.CreateClient();
        (await anonymous.GetAsync(new Uri("/api/v1/workflows/inbox", UriKind.Relative), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var employee = await LoginAsync("dev.employee", withAntiforgery: false);
        (await employee.PostAsJsonAsync(new Uri("/api/v1/workflows", UriKind.Relative), new { definitionCode = "IDP_APPROVAL", subjectType = "Idp" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> LoginAsync(string subject, bool withAntiforgery = true)
    {
        var client = host.Factory.CreateClient();
        (await client.PostAsJsonAsync(new Uri("/bff/fake-login", UriKind.Relative), new { subject }, Ct)).EnsureSuccessStatusCode();
        if (withAntiforgery)
        {
            var token = await client.GetFromJsonAsync<JsonElement>(new Uri("/bff/antiforgery", UriKind.Relative), Ct);
            client.DefaultRequestHeaders.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());
        }

        return client;
    }

    private static async Task<JsonElement> ActAsync(HttpClient client, string? id, string action, string? comment = null)
    {
        var response = await client.PostAsJsonAsync(new Uri($"/api/v1/workflows/{id}/actions", UriKind.Relative), new { action, comment }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Ct), Web);
}
