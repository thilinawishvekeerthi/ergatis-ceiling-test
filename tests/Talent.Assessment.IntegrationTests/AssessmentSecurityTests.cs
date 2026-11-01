using System.Net;
using System.Net.Http.Json;
using static Talent.Assessment.IntegrationTests.AssessmentHostFixture;

namespace Talent.Assessment.IntegrationTests;

/// <summary>Security tests for every Assessment endpoint (talent-security skill): 401, 403 by role, and the anti-forgery check.</summary>
public sealed class AssessmentSecurityTests(AssessmentHostFixture host) : IClassFixture<AssessmentHostFixture>
{
    public static TheoryData<string> Endpoints =>
    [
        "list-cycles", "create-cycle", "get-cycle", "open", "calibration", "close",
        "list-reviews", "assign", "board", "get-review", "rate", "calibrate",
    ];

    private static Task<HttpResponseMessage> CallAsync(HttpClient client, string endpoint, Guid cycle, Guid review, string? employee = null) => endpoint switch
    {
        "list-cycles" => client.GetAsync("/api/v1/assessment/cycles", Ct),
        "create-cycle" => client.PostAsJsonAsync("/api/v1/assessment/cycles", new { name = $"C {Guid.NewGuid():N}", periodStart = "2026-01-01", periodEnd = "2026-12-31" }, Ct),
        "get-cycle" => client.GetAsync($"/api/v1/assessment/cycles/{cycle}", Ct),
        "open" => client.PostAsync($"/api/v1/assessment/cycles/{cycle}/open", null, Ct),
        "calibration" => client.PostAsync($"/api/v1/assessment/cycles/{cycle}/calibration", null, Ct),
        "close" => client.PostAsync($"/api/v1/assessment/cycles/{cycle}/close", null, Ct),
        "list-reviews" => client.GetAsync($"/api/v1/assessment/cycles/{cycle}/reviews", Ct),
        "assign" => client.PostAsJsonAsync($"/api/v1/assessment/cycles/{cycle}/reviews", new { employeeId = Guid.NewGuid(), reviewerId = Guid.NewGuid() }, Ct),
        "board" => client.GetAsync($"/api/v1/assessment/cycles/{cycle}/board", Ct),
        "get-review" => client.GetAsync($"/api/v1/assessment/reviews/{review}", Ct),
        "rate" => client.PutAsJsonAsync($"/api/v1/assessment/reviews/{review}/rating", new { performanceLevel = 3, potentialLevel = 2 }, Ct),
        "calibrate" => client.PostAsJsonAsync($"/api/v1/assessment/reviews/{review}/calibrate", new { column = 2, row = 2, reason = "x" }, Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
    };

    private async Task<(Guid Cycle, Guid Review)> SeedAsync()
    {
        using var admin = await host.LoginAsync(Admin);
        var cycle = (await ReadAsync(await admin.PostAsJsonAsync("/api/v1/assessment/cycles",
            new { name = $"Sec {Guid.NewGuid():N}", periodStart = "2026-01-01", periodEnd = "2026-12-31" }, Ct))).GetProperty("id").GetGuid();
        var review = (await ReadAsync(await admin.PostAsJsonAsync($"/api/v1/assessment/cycles/{cycle}/reviews",
            new { employeeId = host.Employees[Employee], reviewerId = host.Employees[Manager] }, Ct))).GetProperty("id").GetGuid();
        return (cycle, review);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutSession_Returns401(string endpoint)
    {
        var (cycle, review) = await SeedAsync();
        using var client = host.Anonymous();

        var response = await CallAsync(client, endpoint, cycle, review);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("create-cycle")]
    [InlineData("open")]
    [InlineData("calibration")]
    [InlineData("close")]
    [InlineData("assign")]
    [InlineData("calibrate")]
    public async Task AdminActions_AreForbiddenForEmployeeAndManager(string endpoint)
    {
        var (cycle, review) = await SeedAsync();

        foreach (var user in new[] { Employee, Manager })
        {
            using var client = await host.LoginAsync(user);
            (await CallAsync(client, endpoint, cycle, review)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{endpoint} as {user}");
        }
    }

    [Theory]
    [InlineData("rate", Admin)]
    [InlineData("rate", Committee)]
    [InlineData("board", Employee)]
    [InlineData("list-cycles", Admin)]
    public async Task Rate_Board_AndList_FollowTheMatrix(string endpoint, string user)
    {
        var (cycle, review) = await SeedAsync();
        using var client = await host.LoginAsync(user);

        var response = await CallAsync(client, endpoint, cycle, review);

        // Admin and committee hold no Edit on reviews; employees hold no board view; everyone may list cycles.
        response.StatusCode.ShouldBe(endpoint == "list-cycles" ? HttpStatusCode.OK : HttpStatusCode.Forbidden, $"{endpoint} as {user}");
    }

    [Theory]
    [InlineData("create-cycle")]
    [InlineData("open")]
    [InlineData("assign")]
    [InlineData("rate")]
    [InlineData("calibrate")]
    public async Task StateChangingCall_WithoutAntiforgeryHeader_Returns400(string endpoint)
    {
        var (cycle, review) = await SeedAsync();
        using var client = await host.LoginAsync(Admin, withAntiforgery: false);

        (await CallAsync(client, endpoint, cycle, review)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnotherEmployee_CannotReadSomeoneElsesReview()
    {
        var (_, review) = await SeedAsync();
        using var other = await host.LoginAsync(OtherEmployee);

        (await CallAsync(other, "get-review", Guid.Empty, review)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
