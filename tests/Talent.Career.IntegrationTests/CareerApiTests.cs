using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Talent.Career.IntegrationTests.CareerHostFixture;

namespace Talent.Career.IntegrationTests;

/// <summary>The Career API end to end on a real host and database: catalogue, paths, profiles and the Career Centre.</summary>
public sealed class CareerApiTests(CareerHostFixture host) : IClassFixture<CareerHostFixture>
{
    private static string NewCode() => $"JOB_{Guid.NewGuid():N}"[..24].ToUpperInvariant();

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await ReadAsync(response);
    }

    private static object Req(string code, int level, int? weight = null) => new { competencyCode = code, requiredLevel = level, weight };

    private static async Task<Guid> NewJobAsync(HttpClient admin, string title, int level, params object[] requirements)
    {
        var job = await OkAsync(
            await admin.PostAsJsonAsync("/api/v1/career/jobs", new { code = NewCode(), title, level, description = $"About {title}", requirements }, Ct),
            HttpStatusCode.Created);
        return job.GetProperty("id").GetGuid();
    }

    private static async Task AddPathAsync(HttpClient admin, Guid from, Guid to) =>
        (await admin.PostAsJsonAsync("/api/v1/career/paths", new { fromJobId = from, toJobId = to }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

    private async Task<HttpResponseMessage> SetProfileAsync(HttpClient client, string employee, Guid? currentJob, params (string Code, int Level)[] competencies) =>
        await client.PutAsJsonAsync($"/api/v1/career/employees/{host.Employees[employee]}/profile",
            new { currentJobId = currentJob, competencies = competencies.Select(c => new { competencyCode = c.Code, level = c.Level }) }, Ct);

    // ---- catalogue ----

    [Fact]
    public async Task CreateJob_ReturnsItWithSortedRequirements()
    {
        using var admin = await host.LoginAsync(Admin);

        var job = await OkAsync(
            await admin.PostAsJsonAsync("/api/v1/career/jobs",
                new { code = NewCode(), title = "  Data Engineer ", level = 4, requirements = new[] { Req("SQL", 4, 2), Req("PYTHON", 3) } }, Ct),
            HttpStatusCode.Created);

        job.GetProperty("title").GetString().ShouldBe("Data Engineer");
        job.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        job.GetProperty("requirements").EnumerateArray().Select(r => r.GetProperty("competencyCode").GetString()).ShouldBe(["PYTHON", "SQL"]);
        job.GetProperty("requirements")[0].GetProperty("weight").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task CreateJob_WhenCodeTaken_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var code = NewCode();
        var body = new { code, title = "Tester", level = 2, requirements = Array.Empty<object>() };
        (await admin.PostAsJsonAsync("/api/v1/career/jobs", body, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await admin.PostAsJsonAsync("/api/v1/career/jobs", body, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateJob_WhenBodyInvalid_Returns400WithFieldErrors()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/career/jobs",
            new { code = NewCode(), title = "", level = 99, requirements = new[] { Req("bad code", 9) } }, Ct);

        var problem = await OkAsync(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        errors.GetProperty("title").GetArrayLength().ShouldBeGreaterThan(0);
        errors.GetProperty("level").GetArrayLength().ShouldBeGreaterThan(0);
        errors.EnumerateObject().Any(e => e.Name.StartsWith("requirements", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateJob_WhenCompetencyRepeated_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/career/jobs",
            new { code = NewCode(), title = "Dup", level = 2, requirements = new[] { Req("SQL", 2), Req("SQL", 3) } }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateJob_ReplacesRequirements_AndInactiveJobsLeaveTheDefaultList()
    {
        using var admin = await host.LoginAsync(Admin);
        var id = await NewJobAsync(admin, "Old title", 3, Req("SQL", 2), Req("LEADERSHIP", 2));

        var updated = await OkAsync(await admin.PutAsJsonAsync($"/api/v1/career/jobs/{id}",
            new { title = "New title", level = 4, isActive = false, requirements = new[] { Req("SQL", 5, 3), Req("ARCHITECTURE", 3) } }, Ct));

        updated.GetProperty("title").GetString().ShouldBe("New title");
        updated.GetProperty("requirements").EnumerateArray().Select(r => r.GetProperty("competencyCode").GetString()).ShouldBe(["ARCHITECTURE", "SQL"]);
        var active = await OkAsync(await admin.GetAsync("/api/v1/career/jobs", Ct));
        active.EnumerateArray().Any(j => j.GetProperty("id").GetGuid() == id).ShouldBeFalse();
        var all = await OkAsync(await admin.GetAsync("/api/v1/career/jobs?includeInactive=true", Ct));
        all.EnumerateArray().Any(j => j.GetProperty("id").GetGuid() == id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetJob_ByEmployee_Works_AndUnknownIdIs404ForAdmin()
    {
        using var admin = await host.LoginAsync(Admin);
        var id = await NewJobAsync(admin, "Visible job", 2);
        using var employee = await host.LoginAsync(Employee);

        (await OkAsync(await employee.GetAsync($"/api/v1/career/jobs/{id}", Ct))).GetProperty("title").GetString().ShouldBe("Visible job");
        (await admin.GetAsync($"/api/v1/career/jobs/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.PutAsJsonAsync($"/api/v1/career/jobs/{Guid.NewGuid()}",
            new { title = "x", level = 1, requirements = Array.Empty<object>() }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- paths ----

    [Fact]
    public async Task AddPath_ReturnsTitles_AndAppearsInTheList_ThenCanBeRemoved()
    {
        using var admin = await host.LoginAsync(Admin);
        var junior = await NewJobAsync(admin, "Junior X", 1);
        var senior = await NewJobAsync(admin, "Senior X", 3);

        var path = await OkAsync(await admin.PostAsJsonAsync("/api/v1/career/paths", new { fromJobId = junior, toJobId = senior }, Ct), HttpStatusCode.Created);

        path.GetProperty("fromTitle").GetString().ShouldBe("Junior X");
        path.GetProperty("toTitle").GetString().ShouldBe("Senior X");
        using var employee = await host.LoginAsync(Employee);
        var list = await OkAsync(await employee.GetAsync("/api/v1/career/paths", Ct));
        var pathId = path.GetProperty("id").GetGuid();
        list.EnumerateArray().Any(p => p.GetProperty("id").GetGuid() == pathId).ShouldBeTrue();

        (await admin.DeleteAsync($"/api/v1/career/paths/{pathId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await OkAsync(await admin.GetAsync("/api/v1/career/paths", Ct))).EnumerateArray().Any(p => p.GetProperty("id").GetGuid() == pathId).ShouldBeFalse();
    }

    [Fact]
    public async Task AddPath_RejectsDuplicateSelfLoopAndUnknownJob()
    {
        using var admin = await host.LoginAsync(Admin);
        var a = await NewJobAsync(admin, "Loop A", 1);
        var b = await NewJobAsync(admin, "Loop B", 2);
        await AddPathAsync(admin, a, b);

        async Task<HttpStatusCode> Add(Guid from, Guid to) =>
            (await admin.PostAsJsonAsync("/api/v1/career/paths", new { fromJobId = from, toJobId = to }, Ct)).StatusCode;

        (await Add(a, b)).ShouldBe(HttpStatusCode.Conflict);
        (await Add(b, a)).ShouldBe(HttpStatusCode.Conflict);
        (await Add(a, a)).ShouldBe(HttpStatusCode.BadRequest);
        (await Add(a, Guid.NewGuid())).ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- profile and Career Centre ----

    [Fact]
    public async Task CareerCentre_ShowsOpportunitiesByStepsWithMatchAndGaps()
    {
        using var admin = await host.LoginAsync(Admin);
        var junior = await NewJobAsync(admin, "Junior", 1, Req("SQL", 2));
        var mid = await NewJobAsync(admin, "Mid", 2, Req("SQL", 4), Req("LEADERSHIP", 2));
        var lead = await NewJobAsync(admin, "Lead", 3, Req("SQL", 5), Req("LEADERSHIP", 4));
        await AddPathAsync(admin, junior, mid);
        await AddPathAsync(admin, mid, lead);
        (await SetProfileAsync(admin, Employee, junior, ("SQL", 2), ("LEADERSHIP", 1))).EnsureSuccessStatusCode();
        using var employee = await host.LoginAsync(Employee);

        var centre = await OkAsync(await employee.GetAsync("/api/v1/career/me/centre", Ct));

        centre.GetProperty("currentJob").GetProperty("title").GetString().ShouldBe("Junior");
        centre.GetProperty("competencies").EnumerateArray().Select(c => c.GetProperty("competencyCode").GetString()).ShouldBe(["LEADERSHIP", "SQL"]);
        var ops = centre.GetProperty("opportunities").EnumerateArray().ToList();
        ops.Select(o => o.GetProperty("job").GetProperty("title").GetString()).ShouldBe(["Mid", "Lead"]);
        ops.Select(o => o.GetProperty("steps").GetInt32()).ShouldBe([1, 2]);

        // Mid: SQL 2/4 = 0.5, LEADERSHIP 1/2 = 0.5 -> 50%. Lead: SQL 2/5 = 0.4, LEADERSHIP 1/4 = 0.25 -> 32.5%
        ops[0].GetProperty("matchPercent").GetDecimal().ShouldBe(50m);
        ops[1].GetProperty("matchPercent").GetDecimal().ShouldBe(32.5m);
        var sql = ops[0].GetProperty("requirements").EnumerateArray().Single(r => r.GetProperty("competencyCode").GetString() == "SQL");
        (sql.GetProperty("requiredLevel").GetInt32(), sql.GetProperty("currentLevel").GetInt32(), sql.GetProperty("gap").GetInt32(), sql.GetProperty("met").GetBoolean())
            .ShouldBe((4, 2, 2, false));
    }

    [Fact]
    public async Task CareerCentre_SkipsInactiveJobs_AndJobsMoreThanThreeStepsAway()
    {
        using var admin = await host.LoginAsync(Admin);
        var j0 = await NewJobAsync(admin, "Step 0", 1);
        var j1 = await NewJobAsync(admin, "Step 1", 2);
        var j2 = await NewJobAsync(admin, "Step 2", 3);
        var j3 = await NewJobAsync(admin, "Step 3", 4);
        var j4 = await NewJobAsync(admin, "Step 4", 5);
        var hidden = await NewJobAsync(admin, "Retired step", 3);
        await AddPathAsync(admin, j0, j1);
        await AddPathAsync(admin, j1, j2);
        await AddPathAsync(admin, j2, j3);
        await AddPathAsync(admin, j3, j4);
        await AddPathAsync(admin, j0, hidden);
        (await admin.PutAsJsonAsync($"/api/v1/career/jobs/{hidden}",
            new { title = "Retired step", level = 3, isActive = false, requirements = Array.Empty<object>() }, Ct)).EnsureSuccessStatusCode();
        (await SetProfileAsync(admin, Employee, j0)).EnsureSuccessStatusCode();
        using var employee = await host.LoginAsync(Employee);

        var centre = await OkAsync(await employee.GetAsync("/api/v1/career/me/centre", Ct));

        centre.GetProperty("opportunities").EnumerateArray().Select(o => o.GetProperty("job").GetProperty("title").GetString())
            .ShouldBe(["Step 1", "Step 2", "Step 3"]);
    }

    [Fact]
    public async Task CareerCentre_WithNoCurrentJob_ListsActiveJobsWithoutSteps()
    {
        using var admin = await host.LoginAsync(Admin);
        var id = await NewJobAsync(admin, "Open to all", 2, Req("SQL", 2));
        (await SetProfileAsync(admin, OtherEmployee, null, ("SQL", 2))).EnsureSuccessStatusCode();
        using var other = await host.LoginAsync(OtherEmployee);

        var centre = await OkAsync(await other.GetAsync("/api/v1/career/me/centre", Ct));

        centre.GetProperty("currentJob").ValueKind.ShouldBe(JsonValueKind.Null);
        var mine = centre.GetProperty("opportunities").EnumerateArray().Single(o => o.GetProperty("job").GetProperty("id").GetGuid() == id);
        mine.GetProperty("steps").ValueKind.ShouldBe(JsonValueKind.Null);
        mine.GetProperty("matchPercent").GetDecimal().ShouldBe(100m);
    }

    [Fact]
    public async Task CareerCentre_WithNoProfile_IsEmpty_AndManagersHaveNoProfileGrant()
    {
        using var manager = await host.LoginAsync(Manager);

        // The manager role has no Profile grant at all, so this is a permission answer, not an empty page.
        (await manager.GetAsync("/api/v1/career/me/centre", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var admin = await host.LoginAsync(Admin);
        var centre = await OkAsync(await admin.GetAsync($"/api/v1/career/employees/{host.Employees[Committee]}/centre", Ct));
        centre.GetProperty("competencies").GetArrayLength().ShouldBe(0);
        centre.GetProperty("currentJob").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ReplaceProfile_ReplacesCompetencies_AndAuditsConfidentially()
    {
        using var admin = await host.LoginAsync(Admin);
        (await SetProfileAsync(admin, Employee, null, ("SQL", 2), ("LEADERSHIP", 3))).EnsureSuccessStatusCode();

        var second = await OkAsync(await SetProfileAsync(admin, Employee, null, ("SQL", 4), ("ARCHITECTURE", 1)));

        second.GetProperty("competencies").EnumerateArray().Select(c => (c.GetProperty("competencyCode").GetString(), c.GetProperty("level").GetInt32()))
            .ShouldBe([("ARCHITECTURE", 1), ("SQL", 4)]);
        host.CountAudit("ReplaceProfile", Admin, confidential: true).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ReplaceProfile_RejectsBadLevelsUnknownJobAndEditingYourOwn()
    {
        using var admin = await host.LoginAsync(Admin);

        (await SetProfileAsync(admin, Employee, null, ("SQL", 9))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SetProfileAsync(admin, Employee, null, ("SQL", 1), ("SQL", 2))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SetProfileAsync(admin, Employee, Guid.NewGuid())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await SetProfileAsync(admin, Admin, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- who may see a Career Centre ----

    [Fact]
    public async Task CareerCentre_OfAnotherEmployee_IsForbiddenExceptForHrAndCommittee()
    {
        using var admin = await host.LoginAsync(Admin);
        (await SetProfileAsync(admin, Employee, null, ("SQL", 3))).EnsureSuccessStatusCode();
        var path = $"/api/v1/career/employees/{host.Employees[Employee]}/centre";

        using var other = await host.LoginAsync(OtherEmployee);
        using var manager = await host.LoginAsync(Manager);
        using var committee = await host.LoginAsync(Committee);
        using var own = await host.LoginAsync(Employee);

        (await other.GetAsync(path, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await manager.GetAsync(path, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await committee.GetAsync(path, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.GetAsync(path, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await own.GetAsync(path, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
