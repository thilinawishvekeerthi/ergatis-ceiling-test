using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Talent.Assessment.IntegrationTests.AssessmentHostFixture;

namespace Talent.Assessment.IntegrationTests;

/// <summary>The Assessment API end to end on a real host and database: the cycle flow, ratings, the board and calibration.</summary>
public sealed class AssessmentApiTests(AssessmentHostFixture host) : IClassFixture<AssessmentHostFixture>
{
    private static string NewName() => $"Cycle {Guid.NewGuid():N}";

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Ct));
        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> CreateCycleAsync(HttpClient admin, string? name = null) =>
        admin.PostAsJsonAsync("/api/v1/assessment/cycles", new { name = name ?? NewName(), periodStart = "2026-01-01", periodEnd = "2026-12-31" }, Ct);

    private static async Task<Guid> NewCycleAsync(HttpClient admin) =>
        (await OkAsync(await CreateCycleAsync(admin), HttpStatusCode.Created)).GetProperty("id").GetGuid();

    private async Task<Guid> AssignAsync(HttpClient admin, Guid cycle, string employee, string reviewer) =>
        (await OkAsync(
            await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{cycle}/reviews",
                new { employeeId = host.Employees[employee], reviewerId = host.Employees[reviewer] }, Ct),
            HttpStatusCode.Created)).GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path) => client.PostAsync(path, null, Ct);

    private static Task<HttpResponseMessage> RateAsync(HttpClient client, Guid review, int performance, int potential) =>
        client.PutAsJsonAsync($"/api/v1/assessment/reviews/{review}/rating", new { performanceLevel = performance, potentialLevel = potential, comment = "Solid" }, Ct);

    /// <summary>Cycle with the Employee reviewed by Manager, already open.</summary>
    private async Task<(Guid Cycle, Guid Review)> OpenCycleWithReviewAsync(HttpClient admin)
    {
        var cycle = await NewCycleAsync(admin);
        var review = await AssignAsync(admin, cycle, Employee, Manager);
        (await PostAsync(admin, $"/api/v1/assessment/cycles/{cycle}/open")).StatusCode.ShouldBe(HttpStatusCode.OK);
        return (cycle, review);
    }

    // ---- cycles ----

    [Fact]
    public async Task CreateCycle_PinsLatestPublishedScalesAndGrid()
    {
        using var admin = await host.LoginAsync(Admin);

        var cycle = await OkAsync(await CreateCycleAsync(admin), HttpStatusCode.Created);

        cycle.GetProperty("status").GetString().ShouldBe("Draft");
        cycle.GetProperty("performanceScaleVersionId").GetGuid().ShouldNotBe(Guid.Empty);
        cycle.GetProperty("gridVersionId").GetGuid().ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCycle_WhenNameTaken_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var name = NewName();
        (await CreateCycleAsync(admin, name)).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await CreateCycleAsync(admin, name)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCycle_WhenBodyInvalid_Returns400WithFieldErrors()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/assessment/cycles", new { name = "", periodStart = "2026-12-31", periodEnd = "2026-01-01" }, Ct);

        var problem = await OkAsync(response, HttpStatusCode.BadRequest);
        problem.GetProperty("errors").GetProperty("name").GetArrayLength().ShouldBeGreaterThan(0);
        problem.GetProperty("errors").GetProperty("periodEnd").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreateCycle_WhenScaleCodeUnknown_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);

        var response = await admin.PostAsJsonAsync("/api/v1/assessment/cycles",
            new { name = NewName(), periodStart = "2026-01-01", periodEnd = "2026-12-31", performanceScaleCode = "NO_SUCH_SCALE" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OpenCycle_LocksThePinnedConfigVersions()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycleId = await NewCycleAsync(admin);

        var opened = await OkAsync(await PostAsync(admin, $"/api/v1/assessment/cycles/{cycleId}/open"));

        opened.GetProperty("status").GetString().ShouldBe("Open");
        foreach (var field in new[] { "performanceScaleVersionId", "potentialScaleVersionId", "gridVersionId" })
        {
            var version = await OkAsync(await admin.GetAsync($"/api/v1/config/versions/{opened.GetProperty(field).GetGuid()}", Ct));
            version.GetProperty("status").GetString().ShouldBe("Locked", field);
        }
    }

    [Fact]
    public async Task CycleLifecycle_RejectsOutOfOrderMoves()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycleId = await NewCycleAsync(admin);

        (await PostAsync(admin, $"/api/v1/assessment/cycles/{cycleId}/calibration")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await PostAsync(admin, $"/api/v1/assessment/cycles/{cycleId}/close")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await PostAsync(admin, $"/api/v1/assessment/cycles/{cycleId}/open")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostAsync(admin, $"/api/v1/assessment/cycles/{cycleId}/open")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListCycles_IncludesReviewCountsForTheAdmin()
    {
        using var admin = await host.LoginAsync(Admin);
        var (cycle, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);
        (await RateAsync(manager, review, 4, 2)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await OkAsync(await admin.GetAsync("/api/v1/assessment/cycles", Ct));

        var row = list.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == cycle);
        row.GetProperty("reviewCount").GetInt32().ShouldBe(1);
        row.GetProperty("ratedCount").GetInt32().ShouldBe(1);
    }

    // ---- assigning ----

    [Fact]
    public async Task Assign_WhenEmployeeAlreadyInCycle_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = await NewCycleAsync(admin);
        await AssignAsync(admin, cycle, Employee, Manager);

        var again = await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{cycle}/reviews",
            new { employeeId = host.Employees[Employee], reviewerId = host.Employees[OtherManager] }, Ct);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Assign_WhenReviewerIsTheEmployee_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = await NewCycleAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{cycle}/reviews",
            new { employeeId = host.Employees[Manager], reviewerId = host.Employees[Manager] }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assign_WhenCycleInCalibration_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var (cycle, _) = await OpenCycleWithReviewAsync(admin);
        await PostAsync(admin, $"/api/v1/assessment/cycles/{cycle}/calibration");

        var response = await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{cycle}/reviews",
            new { employeeId = host.Employees[OtherEmployee], reviewerId = host.Employees[Manager] }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---- rating ----

    [Fact]
    public async Task Rate_ByAssignedManager_PlacesOnTheNineBox_AndAuditsConfidentially()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);

        var rated = await OkAsync(await RateAsync(manager, review, 5, 3));

        rated.GetProperty("status").GetString().ShouldBe("Rated");
        rated.GetProperty("performanceLabel").GetString().ShouldBe("Outstanding");
        rated.GetProperty("finalColumn").GetInt32().ShouldBe(2);
        rated.GetProperty("finalRow").GetInt32().ShouldBe(2);
        host.CountAudit("Rate", Manager, confidential: true).ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(1, 1, 0, 0)]
    [InlineData(3, 2, 1, 1)]
    [InlineData(4, 1, 2, 0)]
    [InlineData(2, 3, 0, 2)]
    public async Task Rate_PlacesPerformanceOnColumnsAndPotentialOnRows(int performance, int potential, int column, int row)
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);

        var rated = await OkAsync(await RateAsync(manager, review, performance, potential));

        (rated.GetProperty("finalColumn").GetInt32(), rated.GetProperty("finalRow").GetInt32()).ShouldBe((column, row));
    }

    [Fact]
    public async Task Rate_WhenLevelNotOnScale_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);

        (await RateAsync(manager, review, 9, 2)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RateAsync(manager, review, 3, 4)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rate_WhenCycleStillDraft_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = await NewCycleAsync(admin);
        var review = await AssignAsync(admin, cycle, Employee, Manager);
        using var manager = await host.LoginAsync(Manager);

        (await RateAsync(manager, review, 3, 2)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Rate_ByManagerWhoIsNotTheReviewer_Returns403()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var other = await host.LoginAsync(OtherManager);

        (await RateAsync(other, review, 3, 2)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rate_ByTheEmployeeThemselves_Returns403()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var employee = await host.LoginAsync(Employee);

        (await RateAsync(employee, review, 5, 3)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rate_AgainWhileOpen_ReplacesTheEarlierRating()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);
        await RateAsync(manager, review, 5, 3);

        var second = await OkAsync(await RateAsync(manager, review, 1, 1));

        second.GetProperty("finalColumn").GetInt32().ShouldBe(0);
        second.GetProperty("finalRow").GetInt32().ShouldBe(0);
    }

    // ---- visibility and masking ----

    [Fact]
    public async Task EmployeeReadingOwnReview_SeesPerformanceButNotPotentialOrGridCell()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);
        await RateAsync(manager, review, 4, 3);
        using var employee = await host.LoginAsync(Employee);

        var own = await OkAsync(await employee.GetAsync($"/api/v1/assessment/reviews/{review}", Ct));

        own.GetProperty("performanceLevel").GetInt32().ShouldBe(4);
        own.GetProperty("comment").GetString().ShouldBe("Solid");
        own.GetProperty("potentialLevel").ValueKind.ShouldBe(JsonValueKind.Null);
        own.GetProperty("finalColumn").ValueKind.ShouldBe(JsonValueKind.Null);
        own.GetProperty("finalRow").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ManagerReadingAssignedReview_SeesPotentialAndGridCell()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);
        await RateAsync(manager, review, 4, 3);

        var seen = await OkAsync(await manager.GetAsync($"/api/v1/assessment/reviews/{review}", Ct));

        seen.GetProperty("potentialLevel").GetInt32().ShouldBe(3);
        seen.GetProperty("finalColumn").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task GetReview_ByAnotherEmployee_Returns403()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var other = await host.LoginAsync(OtherEmployee);

        (await other.GetAsync($"/api/v1/assessment/reviews/{review}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetReview_ByManagerWhoIsNotAssigned_Returns403()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var other = await host.LoginAsync(OtherManager);

        (await other.GetAsync($"/api/v1/assessment/reviews/{review}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListReviews_ShowsEachCallerOnlyTheirOwnScope()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = await NewCycleAsync(admin);
        var mine = await AssignAsync(admin, cycle, Employee, Manager);
        var theirs = await AssignAsync(admin, cycle, OtherEmployee, OtherManager);

        static HashSet<Guid> Ids(JsonElement list) => [.. list.EnumerateArray().Select(r => r.GetProperty("id").GetGuid())];
        async Task<HashSet<Guid>> ListAs(string user)
        {
            using var client = await host.LoginAsync(user);
            return Ids(await OkAsync(await client.GetAsync($"/api/v1/assessment/cycles/{cycle}/reviews", Ct)));
        }

        (await ListAs(Admin)).ShouldBe([mine, theirs], ignoreOrder: true);
        (await ListAs(Committee)).ShouldBe([mine, theirs], ignoreOrder: true);
        (await ListAs(Manager)).ShouldBe([mine]);
        (await ListAs(OtherManager)).ShouldBe([theirs]);
        (await ListAs(Employee)).ShouldBe([mine]);
        (await ListAs(OtherEmployee)).ShouldBe([theirs]);
    }

    // ---- board ----

    [Fact]
    public async Task Board_ForManager_ShowsOnlyTheirRatedPeopleOnTheGrid()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = await NewCycleAsync(admin);
        var mine = await AssignAsync(admin, cycle, Employee, Manager);
        var theirs = await AssignAsync(admin, cycle, OtherEmployee, OtherManager);
        await PostAsync(admin, $"/api/v1/assessment/cycles/{cycle}/open");
        using var manager = await host.LoginAsync(Manager);
        using var other = await host.LoginAsync(OtherManager);
        await RateAsync(manager, mine, 5, 3);
        await RateAsync(other, theirs, 1, 1);

        var board = await OkAsync(await manager.GetAsync($"/api/v1/assessment/cycles/{cycle}/board", Ct));

        board.GetProperty("layout").GetProperty("cells").GetArrayLength().ShouldBe(9);
        board.GetProperty("mode").GetString().ShouldBe("CalculatedWithOverride");
        var people = board.GetProperty("people").EnumerateArray().ToList();
        people.Count.ShouldBe(1);
        people[0].GetProperty("reviewId").GetGuid().ShouldBe(mine);
        (people[0].GetProperty("column").GetInt32(), people[0].GetProperty("row").GetInt32()).ShouldBe((2, 2));
    }

    [Fact]
    public async Task Board_ForCommittee_ShowsEveryoneRated_AndSkipsUnrated()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = await NewCycleAsync(admin);
        var rated = await AssignAsync(admin, cycle, Employee, Manager);
        await AssignAsync(admin, cycle, OtherEmployee, OtherManager);
        await PostAsync(admin, $"/api/v1/assessment/cycles/{cycle}/open");
        using var manager = await host.LoginAsync(Manager);
        await RateAsync(manager, rated, 3, 2);
        using var committee = await host.LoginAsync(Committee);

        var board = await OkAsync(await committee.GetAsync($"/api/v1/assessment/cycles/{cycle}/board", Ct));

        board.GetProperty("people").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Board_ForEmployee_Returns403()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = await NewCycleAsync(admin);
        using var employee = await host.LoginAsync(Employee);

        (await employee.GetAsync($"/api/v1/assessment/cycles/{cycle}/board", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- calibration ----

    private async Task<(Guid Cycle, Guid Review)> RatedCycleInCalibrationAsync(HttpClient admin)
    {
        var (cycle, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);
        await RateAsync(manager, review, 3, 2); // lands on (1,1)
        (await PostAsync(admin, $"/api/v1/assessment/cycles/{cycle}/calibration")).StatusCode.ShouldBe(HttpStatusCode.OK);
        return (cycle, review);
    }

    private static Task<HttpResponseMessage> CalibrateAsync(HttpClient client, Guid review, int column, int row, string? reason) =>
        client.PostAsJsonAsync($"/api/v1/assessment/reviews/{review}/calibrate", new { column, row, reason }, Ct);

    [Fact]
    public async Task Calibrate_ByCommittee_MovesFinalCellKeepsProposedAndAuditsConfidentially()
    {
        using var admin = await host.LoginAsync(Admin);
        var (cycle, review) = await RatedCycleInCalibrationAsync(admin);
        using var committee = await host.LoginAsync(Committee);

        var moved = await OkAsync(await CalibrateAsync(committee, review, 2, 2, "Led the platform migration"));

        moved.GetProperty("finalColumn").GetInt32().ShouldBe(2);
        moved.GetProperty("proposedColumn").GetInt32().ShouldBe(1);
        moved.GetProperty("calibratedBy").GetString().ShouldBe(Committee);
        moved.GetProperty("calibrationReason").GetString().ShouldBe("Led the platform migration");
        host.CountAudit("Calibrate", Committee, confidential: true).ShouldBeGreaterThan(0);

        var board = await OkAsync(await committee.GetAsync($"/api/v1/assessment/cycles/{cycle}/board", Ct));
        var person = board.GetProperty("people").EnumerateArray().Single();
        person.GetProperty("calibrated").GetBoolean().ShouldBeTrue();
        person.GetProperty("proposedColumn").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Calibrate_WithoutReason_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await RatedCycleInCalibrationAsync(admin);
        using var committee = await host.LoginAsync(Committee);

        var response = await CalibrateAsync(committee, review, 2, 2, " ");

        var problem = await OkAsync(response, HttpStatusCode.BadRequest);
        problem.GetProperty("errors").GetProperty("reason").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Calibrate_OutsideTheGrid_Returns400()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await RatedCycleInCalibrationAsync(admin);
        using var committee = await host.LoginAsync(Committee);

        (await CalibrateAsync(committee, review, 3, 0, "Reason")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Calibrate_ByManager_Returns403()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await RatedCycleInCalibrationAsync(admin);
        using var manager = await host.LoginAsync(Manager);

        (await CalibrateAsync(manager, review, 2, 2, "Reason")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Calibrate_WhileCycleStillOpen_Returns409()
    {
        using var admin = await host.LoginAsync(Admin);
        var (_, review) = await OpenCycleWithReviewAsync(admin);
        using var manager = await host.LoginAsync(Manager);
        await RateAsync(manager, review, 3, 2);
        using var committee = await host.LoginAsync(Committee);

        (await CalibrateAsync(committee, review, 2, 2, "Reason")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AfterCalibrationStarts_RatingIsClosed_AndAfterCloseNothingMoves()
    {
        using var admin = await host.LoginAsync(Admin);
        var (cycle, review) = await RatedCycleInCalibrationAsync(admin);
        using var manager = await host.LoginAsync(Manager);
        using var committee = await host.LoginAsync(Committee);

        (await RateAsync(manager, review, 5, 3)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await PostAsync(admin, $"/api/v1/assessment/cycles/{cycle}/close")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CalibrateAsync(committee, review, 2, 2, "Too late")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---- not found ----

    [Fact]
    public async Task UnknownIds_Return404ForPrivilegedAnd403ForUnprivilegedCallers()
    {
        using var admin = await host.LoginAsync(Admin);
        using var nobody = await host.LoginAsync(Employee);
        var missing = Guid.NewGuid();

        (await admin.GetAsync($"/api/v1/assessment/cycles/{missing}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.GetAsync($"/api/v1/assessment/reviews/{missing}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PostAsync(nobody, $"/api/v1/assessment/cycles/{missing}/open")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
