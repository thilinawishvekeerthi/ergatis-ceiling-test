using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Talent.Security.IntegrationTests.SecurityHostFixture;

namespace Talent.Security.IntegrationTests;

/// <summary>
/// Security tests for every Config endpoint (talent-security skill): 401, 403, out of scope, masking,
/// missing anti-forgery token, and an audit entry.
/// </summary>
public sealed class ConfigApiSecurityTests(SecurityHostFixture host) : IClassFixture<SecurityHostFixture>
{
    private static readonly Guid SomeId = Guid.NewGuid();

    private static JsonElement Payload => JsonDocument.Parse(ScalePayload).RootElement;

    /// <summary>One entry per endpoint. The call is made against a version that exists, so only security can stop it.</summary>
    public static TheoryData<string> Endpoints =>
    [
        "get-version", "get-latest", "list", "create-draft", "new-version", "update-payload", "publish", "lock",
    ];

    private static async Task<HttpResponseMessage> CallAsync(HttpClient client, string endpoint, string code, Guid versionId, string? type = null)
    {
        type ??= "Scale";
        return endpoint switch
        {
            "get-version" => await client.GetAsync($"/api/v1/config/versions/{versionId}", Ct),
            "get-latest" => await client.GetAsync($"/api/v1/config/{type}/{code}/latest", Ct),
            "list" => await client.GetAsync($"/api/v1/config/{type}", Ct),
            "create-draft" => await client.PostAsJsonAsync($"/api/v1/config/{type}/{NewCode()}/drafts", new { name = "x", payload = Payload }, Ct),
            "new-version" => await client.PostAsync($"/api/v1/config/{type}/{code}/versions", null, Ct),
            "update-payload" => await client.PutAsJsonAsync($"/api/v1/config/versions/{versionId}/payload", new { payload = Payload }, Ct),
            "publish" => await client.PostAsync($"/api/v1/config/versions/{versionId}/publish", null, Ct),
            "lock" => await client.PostAsync($"/api/v1/config/versions/{versionId}/lock", null, Ct),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
        };
    }

    private async Task<(string Code, Guid Id)> SeedAsync()
    {
        using var admin = await host.LoginAsync(Admin);
        return await CreatePublishedScaleAsync(admin);
    }

    // ---- 401 ----

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutSession_Returns401(string endpoint)
    {
        var (code, id) = await SeedAsync();
        using var client = host.Anonymous();

        var response = await CallAsync(client, endpoint, code, id);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull("an API call must not redirect to a login page");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // ---- 403: no permission ----

    [Theory]
    [InlineData("create-draft")]
    [InlineData("new-version")]
    [InlineData("update-payload")]
    [InlineData("publish")]
    [InlineData("lock")]
    public async Task WriteEndpoint_WhenRoleMayOnlyView_Returns403(string endpoint)
    {
        var (code, id) = await SeedAsync();
        using var client = await host.LoginAsync(Manager);

        var response = await CallAsync(client, endpoint, code, id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WhenLoginHasNoEmployee_Returns403(string endpoint)
    {
        var (code, id) = await SeedAsync();
        using var client = await host.LoginAsync(NoEmployee);

        var response = await CallAsync(client, endpoint, code, id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("get-version")]
    [InlineData("get-latest")]
    [InlineData("list")]
    public async Task ReadEndpoint_WhenRoleHasNoRightOnThatType_Returns403(string endpoint)
    {
        using var admin = await host.LoginAsync(Admin);
        var (code, id) = await CreateGridishAsync(admin);
        using var employee = await host.LoginAsync(Employee);

        var response = await CallAsync(employee, endpoint, code, id, "EligibilityRule");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadEndpoint_WhenVersionMissingAndRoleHasNoRights_Returns403NotNotFound()
    {
        using var client = await host.LoginAsync(Nobody);

        var response = await client.GetAsync($"/api/v1/config/versions/{SomeId}", Ct);

        // An unprivileged caller must not learn whether an id exists.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReadEndpoint_WhenVersionMissingAndRoleMayRead_Returns404()
    {
        using var client = await host.LoginAsync(Employee);

        var response = await client.GetAsync($"/api/v1/config/versions/{SomeId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- 403: outside the data scope ----

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WhenGrantReachesOnlyDirectReports_Returns403BecauseConfigIsOrganisationWide(string endpoint)
    {
        var (code, id) = await SeedAsync();
        using var client = await host.LoginAsync(ScopedAdmin);

        var response = await CallAsync(client, endpoint, code, id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- masking ----

    [Fact]
    public async Task GetVersion_ForRoleWithPublicFieldsOnly_MasksPayloadChecksumAndPublisher()
    {
        var (_, id) = await SeedAsync();
        using var employee = await host.LoginAsync(Employee);

        var response = await employee.GetAsync($"/api/v1/config/versions/{id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(Ct);
        var json = JsonSerializer.Deserialize<JsonElement>(body);
        json.GetProperty("status").GetString().ShouldBe("Published");
        json.GetProperty("code").GetString().ShouldNotBeNull();
        IsMasked(json, "payload").ShouldBeTrue(body);
        IsMasked(json, "checksum").ShouldBeTrue(body);
        IsMasked(json, "publishedBy").ShouldBeTrue(body);
        body.ShouldNotContain("minScore", Case.Insensitive, "masked data must not be sent at all");
        body.ShouldNotContain(Admin);
    }

    [Fact]
    public async Task GetLatest_ForRoleWithInternalFields_ReturnsPayload()
    {
        var (code, _) = await SeedAsync();
        using var manager = await host.LoginAsync(Manager);

        var json = await ReadAsync(await manager.GetAsync($"/api/v1/config/Scale/{code}/latest", Ct));

        json.GetProperty("payload").GetProperty("levels").GetArrayLength().ShouldBe(2);
        json.GetProperty("publishedBy").GetString().ShouldBe(Admin);
    }

    [Fact]
    public async Task GetVersion_OfPermissionMatrix_IsHiddenFromManagerButShownToCommittee()
    {
        using var admin = await host.LoginAsync(Admin);
        var latest = await ReadAsync(await admin.GetAsync("/api/v1/config/PermissionMatrix/DEFAULT/latest", Ct));
        var id = latest.GetProperty("id").GetGuid();

        using var manager = await host.LoginAsync(Manager);
        using var committee = await host.LoginAsync(Committee);

        (await manager.GetAsync($"/api/v1/config/versions/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await committee.GetAsync($"/api/v1/config/versions/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- anti-forgery ----

    [Theory]
    [InlineData("create-draft")]
    [InlineData("new-version")]
    [InlineData("update-payload")]
    [InlineData("publish")]
    [InlineData("lock")]
    public async Task WriteEndpoint_WithoutAntiforgeryToken_Returns400(string endpoint)
    {
        var (code, id) = await SeedAsync();
        using var client = await host.LoginAsync(Admin, withAntiforgery: false);

        var response = await CallAsync(client, endpoint, code, id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // ---- audit and the happy path ----

    [Fact]
    public async Task Lifecycle_AsAdmin_WorksAndAuditsTheRealUser()
    {
        using var admin = await host.LoginAsync(Admin);
        var (code, draftId) = await CreateScaleDraftAsync(admin);

        var update = await admin.PutAsJsonAsync($"/api/v1/config/versions/{draftId}/payload", new { payload = Payload }, Ct);
        var publish = await admin.PostAsync($"/api/v1/config/versions/{draftId}/publish", null, Ct);
        var locked = await admin.PostAsync($"/api/v1/config/versions/{draftId}/lock", null, Ct);
        var next = await admin.PostAsync($"/api/v1/config/Scale/{code}/versions", null, Ct);

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(publish)).GetProperty("status").GetString().ShouldBe("Published");
        (await ReadAsync(locked)).GetProperty("status").GetString().ShouldBe("Locked");
        next.StatusCode.ShouldBe(HttpStatusCode.Created);
        var nextJson = await ReadAsync(next);
        nextJson.GetProperty("versionNo").GetInt32().ShouldBe(2);
        nextJson.GetProperty("status").GetString().ShouldBe("Draft");

        foreach (var action in new[] { "CreateDraft", "UpdateDraft", "Publish", "Lock" })
        {
            (await host.CountAuditAsync(action, Admin)).ShouldBeGreaterThan(0, action);
        }
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsNewestVersionPerDefinition()
    {
        using var admin = await host.LoginAsync(Admin);
        var (code, draftId) = await CreateScaleDraftAsync(admin);
        (await admin.PostAsync($"/api/v1/config/versions/{draftId}/publish", null, Ct)).EnsureSuccessStatusCode();
        var next = await ReadAsync(await admin.PostAsync($"/api/v1/config/Scale/{code}/versions", null, Ct));

        var response = await admin.GetAsync("/api/v1/config/Scale", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rows = (await ReadAsync(response)).EnumerateArray().Where(r => r.GetProperty("code").GetString() == code).ToList();
        rows.Count.ShouldBe(1);
        rows[0].GetProperty("id").GetGuid().ShouldBe(next.GetProperty("id").GetGuid());
        rows[0].GetProperty("versionNo").GetInt32().ShouldBe(2);
        rows[0].GetProperty("status").GetString().ShouldBe("Draft");
    }

    [Fact]
    public async Task List_WithUnknownType_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.GetAsync("/api/v1/config/Nope", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- errors are ProblemDetails ----

    [Fact]
    public async Task GetVersion_WhenMissing_Returns404ProblemDetails()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.GetAsync($"/api/v1/config/versions/{SomeId}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await ReadAsync(response)).GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Fact]
    public async Task Publish_WhenAlreadyPublished_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, id) = await CreatePublishedScaleAsync(admin);

        var response = await admin.PostAsync($"/api/v1/config/versions/{id}/publish", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdatePayload_WhenVersionPublished_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, id) = await CreatePublishedScaleAsync(admin);

        var response = await admin.PutAsJsonAsync($"/api/v1/config/versions/{id}/payload", new { payload = Payload }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Publish_WhenPayloadBreaksConfigRules_Returns400WithoutInternals()
    {
        using var admin = await host.LoginAsync(Admin);
        var code = NewCode();
        var created = await admin.PostAsJsonAsync($"/api/v1/config/Scale/{code}/drafts",
            new { name = "Bad", payload = JsonDocument.Parse("""{"levels":[]}""").RootElement }, Ct);
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();

        var response = await admin.PostAsync($"/api/v1/config/versions/{id}/publish", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("   at ");
        body.ShouldNotContain("Talent.Config");
    }

    [Theory]
    [InlineData("Nope", "SOME_CODE")]
    [InlineData("0", "SOME_CODE")]
    [InlineData("Scale", "lowercase")]
    [InlineData("Scale", "X")]
    public async Task CreateDraft_WithBadRouteValues_Returns400(string type, string code)
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/config/{type}/{code}/drafts", new { name = "x", payload = Payload }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDraft_ForNewDefinitionWithoutNameOrPayload_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/config/Scale/{NewCode()}/drafts", new { }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDraft_WithPayloadThatIsNotAnObject_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/config/Scale/{NewCode()}/drafts", new { name = "x", payload = 5 }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NewVersion_WhenDefinitionMissing_Returns404()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsync($"/api/v1/config/Scale/{NewCode()}/versions", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Publish_OfPermissionMatrixThatLocksEveryoneOut_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);
        var payload = JsonDocument.Parse(
            """{"grants":[{"role":"HR_ADMIN","resource":"config:Scale","actions":["View","Edit","Approve"],"scope":"All","fieldClass":"Internal"}]}""").RootElement;
        var draft = await admin.PostAsJsonAsync("/api/v1/config/PermissionMatrix/DEFAULT/drafts", new { payload }, Ct);
        var id = (await ReadAsync(draft)).GetProperty("id").GetGuid();

        var response = await admin.PostAsync($"/api/v1/config/versions/{id}/publish", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static bool IsMasked(JsonElement json, string property) =>
        !json.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    private static async Task<(string Code, Guid Id)> CreateGridishAsync(HttpClient admin)
    {
        var code = NewCode();
        var created = await admin.PostAsJsonAsync($"/api/v1/config/EligibilityRule/{code}/drafts",
            new { name = "Rule", payload = JsonDocument.Parse("""{"rule":"service_years >= 2"}""").RootElement }, Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await ReadAsync(created)).GetProperty("id").GetGuid();
        (await admin.PostAsync($"/api/v1/config/versions/{id}/publish", null, Ct)).EnsureSuccessStatusCode();
        return (code, id);
    }
}
