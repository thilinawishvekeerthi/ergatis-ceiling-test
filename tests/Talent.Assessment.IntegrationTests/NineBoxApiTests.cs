using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Talent.Assessment.Persistence;
using static Talent.Assessment.IntegrationTests.NineBoxHostFixture;

namespace Talent.Assessment.IntegrationTests;

/// <summary>BRD acceptance criteria and data requirements, end to end through the host on real PostgreSQL.</summary>
public sealed class NineBoxApiTests(NineBoxHostFixture host) : IClassFixture<NineBoxHostFixture>
{
    private const string Evaluations = "/api/v1/assessment/nine-box/evaluations";

    // Each test evaluates its own seeded employee, so tests do not depend on each other's data.
    private async Task<(HttpClient Client, Guid EmployeeId)> ReviewerForEmployeeAsync(int index)
    {
        var client = await host.LoginAsync(Reviewer);
        var employees = await EmployeesAsync(client);
        return (client, employees[index].GetProperty("employeeId").GetGuid());
    }

    private static Task<HttpResponseMessage> EvaluateAsync(HttpClient client, Guid employeeId, int? performance, int? potential) =>
        client.PostAsJsonAsync(Evaluations, new { employeeId, performance, potential }, Ct);

    private static async Task<JsonElement> LatestAsync(HttpClient client, Guid employeeId) =>
        (await EmployeesAsync(client)).Single(e => e.GetProperty("employeeId").GetGuid() == employeeId).GetProperty("latest");

    private static async Task<JsonElement> HistoryAsync(HttpClient client, Guid employeeId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/v1/assessment/nine-box/employees/{employeeId}/evaluations", Ct);

    [Fact]
    public async Task Boot_SeedsTwelveEmployeesWithPositionAndDepartment()
    {
        using var client = await host.LoginAsync(Reviewer);

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/assessment/nine-box/employees", Ct);

        page.GetProperty("totalCount").GetInt32().ShouldBe(SeededEmployees);
        foreach (var row in page.GetProperty("items").EnumerateArray())
        {
            row.GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
            row.GetProperty("position").GetString().ShouldNotBeNullOrWhiteSpace();
            row.GetProperty("department").GetString().ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task Grid_HasNineBrdBoxesAndARubricForEveryLevel()
    {
        using var client = await host.LoginAsync(Reviewer);

        var grid = await client.GetFromJsonAsync<JsonElement>("/api/v1/assessment/nine-box/grid", Ct);

        var labels = grid.GetProperty("cells").EnumerateArray()
            .OrderBy(c => c.GetProperty("boxNumber").GetInt32())
            .Select(c => c.GetProperty("label").GetString());
        labels.ShouldBe(["Risk", "Effective", "Trusted pro", "Inconsistent", "Core player", "High performer", "Potential gem", "High potential", "Star"]);
        foreach (var axis in new[] { "performanceLevels", "potentialLevels" })
        {
            var levels = grid.GetProperty(axis).EnumerateArray().ToList();
            levels.Select(l => l.GetProperty("level").GetInt32()).ShouldBe([1, 2, 3]);
            levels.ShouldAllBe(l => !string.IsNullOrWhiteSpace(l.GetProperty("description").GetString()));
        }
    }

    [Fact]
    public async Task Ac1_Performance3Potential3_IsStarScore9_SavedAndVisible()
    {
        var (client, employeeId) = await ReviewerForEmployeeAsync(0);

        var response = await EvaluateAsync(client, employeeId, 3, 3);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await ReadAsync(response);
        created.GetProperty("label").GetString().ShouldBe("Star");
        created.GetProperty("talentScore").GetInt32().ShouldBe(9);
        created.GetProperty("boxNumber").GetInt32().ShouldBe(9);
        created.GetProperty("band").GetString().ShouldBe("HIGH");
        created.GetProperty("evaluatedBy").GetString().ShouldBe(Reviewer);

        var latest = await LatestAsync(client, employeeId);
        latest.GetProperty("evaluationId").GetGuid().ShouldBe(created.GetProperty("evaluationId").GetGuid());
        latest.GetProperty("label").GetString().ShouldBe("Star");
    }

    [Fact]
    public async Task Ac2_Performance1Potential1_IsRiskScore1()
    {
        var (client, employeeId) = await ReviewerForEmployeeAsync(1);

        var created = await ReadAsync(await EvaluateAsync(client, employeeId, 1, 1));

        created.GetProperty("label").GetString().ShouldBe("Risk");
        created.GetProperty("talentScore").GetInt32().ShouldBe(1);
        created.GetProperty("boxNumber").GetInt32().ShouldBe(1);
        created.GetProperty("band").GetString().ShouldBe("LOW");
    }

    [Theory]
    [InlineData(0, 2, "performance")]
    [InlineData(4, 2, "performance")]
    [InlineData(2, 0, "potential")]
    [InlineData(2, 9, "potential")]
    [InlineData(null, 2, "performance")]
    [InlineData(2, null, "potential")]
    public async Task Ac3_LevelOutside1To3_IsRejectedWithClearErrorAndNothingSaved(int? performance, int? potential, string field)
    {
        var (client, employeeId) = await ReviewerForEmployeeAsync(2);

        var response = await EvaluateAsync(client, employeeId, performance, potential);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await ReadAsync(response);
        problem.GetProperty("errors").GetProperty(field)[0].GetString()!.ShouldContain("1");
        (await HistoryAsync(client, employeeId)).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Ac4_TwoEvaluations_NewerIsShownAndBothAreKept()
    {
        var (client, employeeId) = await ReviewerForEmployeeAsync(3);

        var first = await ReadAsync(await EvaluateAsync(client, employeeId, 2, 2));
        host.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await ReadAsync(await EvaluateAsync(client, employeeId, 3, 2));

        (await LatestAsync(client, employeeId)).GetProperty("label").GetString().ShouldBe("High performer");
        var history = await HistoryAsync(client, employeeId);
        history.GetProperty("totalCount").GetInt32().ShouldBe(2);
        history.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("evaluationId").GetGuid())
            .ShouldBe([second.GetProperty("evaluationId").GetGuid(), first.GetProperty("evaluationId").GetGuid()]);
    }

    [Fact]
    public async Task LatestWins_ByEvaluationTime_EvenWhenAnOlderEvaluationIsSavedLast()
    {
        var (client, employeeId) = await ReviewerForEmployeeAsync(4);
        host.Clock.Advance(TimeSpan.FromHours(1));
        await EvaluateAsync(client, employeeId, 3, 3);

        // A save that carries an earlier time (for example a slow request) must not replace the newer placement.
        host.Clock.Advance(TimeSpan.FromMinutes(-30));
        await EvaluateAsync(client, employeeId, 1, 1);
        host.Clock.Advance(TimeSpan.FromMinutes(31));

        (await LatestAsync(client, employeeId)).GetProperty("label").GetString().ShouldBe("Star");
        (await HistoryAsync(client, employeeId)).GetProperty("totalCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Persistence_RoundTripsInputsOutputsReviewerAndTime()
    {
        var (client, employeeId) = await ReviewerForEmployeeAsync(5);
        var created = await ReadAsync(await EvaluateAsync(client, employeeId, 2, 3));
        var id = created.GetProperty("evaluationId").GetGuid();

        await using var db = AssessmentDbContextFactory.Create(host.ConnectionString);
        var row = await db.Evaluations.AsNoTracking().SingleAsync(e => e.Id == id, Ct);
        var pointer = await db.LatestPlacements.AsNoTracking().SingleAsync(l => l.EmployeeId == employeeId, Ct);

        row.EmployeeId.ShouldBe(employeeId);
        (row.PerformanceLevel, row.PotentialLevel).ShouldBe((2, 3));
        (row.BoxNumber, row.BoxLabel, row.TalentScore, row.Band).ShouldBe((8, "High potential", 6, "HIGH"));
        row.ReviewerId.ShouldBe(Reviewer);
        row.EvaluatedAt.ShouldBe(created.GetProperty("evaluatedAt").GetDateTimeOffset());
        row.GridVersionId.ShouldBe(created.GetProperty("gridVersionId").GetGuid());
        pointer.EvaluationId.ShouldBe(id);
    }

    [Fact]
    public async Task Database_RejectsARowThatBreaksTheRules()
    {
        await using var db = AssessmentDbContextFactory.Create(host.ConnectionString);

        // Box 9 with performance 1 and potential 1 is impossible; the check constraint stops it even outside the app.
        var insert = db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO assessment.nine_box_evaluations
              (id, employee_id, grid_version_id, performance_level, potential_level, box_number, box_label, talent_score, band, reviewer_id, evaluated_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 1, 1, 9, 'Star', 1, 'HIGH', 'sql', now())
            """,
            Ct);

        (await Should.ThrowAsync<Npgsql.PostgresException>(insert)).SqlState.ShouldBe(Npgsql.PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task UnknownEmployee_Is404()
    {
        using var client = await host.LoginAsync(Reviewer);

        var response = await EvaluateAsync(client, Guid.NewGuid(), 2, 2);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Security_AnonymousIs401_CommitteeMayViewButNotEvaluate_EmployeeIs403()
    {
        (await host.Anonymous().GetAsync("/api/v1/assessment/nine-box/employees", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var committee = await host.LoginAsync(Committee);
        var employees = await EmployeesAsync(committee);
        employees.Count.ShouldBe(SeededEmployees);
        (await EvaluateAsync(committee, employees[6].GetProperty("employeeId").GetGuid(), 2, 2)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var employee = await host.LoginAsync(Employee);
        (await employee.GetAsync("/api/v1/assessment/nine-box/employees", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}

/// <summary>The checked-in contract (docs/api/nine-box.openapi.yaml) and the running host must agree on paths and operations.</summary>
public sealed class NineBoxContractTests(NineBoxHostFixture host) : IClassFixture<NineBoxHostFixture>
{
    [Fact]
    public async Task GeneratedOpenApi_HasEveryPathAndOperationOfTheContract()
    {
        var contract = await File.ReadAllLinesAsync(Path.Combine(RepoRoot(), "docs", "api", "nine-box.openapi.yaml"), Ct);
        var paths = contract.Where(l => l.StartsWith("  /api/", StringComparison.Ordinal)).Select(l => l.Trim().TrimEnd(':')).ToList();
        var operations = contract.Select(l => l.Trim()).Where(l => l.StartsWith("operationId:", StringComparison.Ordinal))
            .Select(l => l["operationId:".Length..].Trim()).ToList();
        paths.Count.ShouldBe(4);

        var generated = await host.Anonymous().GetFromJsonAsync<JsonElement>("/openapi/v1.json", Ct);

        var generatedPaths = generated.GetProperty("paths").EnumerateObject().ToDictionary(p => p.Name, p => p.Value);
        foreach (var path in paths)
        {
            generatedPaths.ShouldContainKey(path);
        }

        var generatedOperations = generatedPaths.Values
            .SelectMany(p => p.EnumerateObject())
            .Where(o => o.Value.ValueKind == JsonValueKind.Object && o.Value.TryGetProperty("operationId", out _))
            .Select(o => o.Value.GetProperty("operationId").GetString());
        operations.ShouldBeSubsetOf(generatedOperations);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Talent.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Talent.sln not found above the test output folder.");
    }
}
