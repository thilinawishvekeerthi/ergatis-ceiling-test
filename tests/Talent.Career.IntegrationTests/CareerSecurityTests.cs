using System.Net;
using System.Net.Http.Json;
using static Talent.Career.IntegrationTests.CareerHostFixture;

namespace Talent.Career.IntegrationTests;

/// <summary>Security tests for every Career endpoint (talent-security skill): 401, 403 by role, and the anti-forgery check.</summary>
public sealed class CareerSecurityTests(CareerHostFixture host) : IClassFixture<CareerHostFixture>
{
    public static TheoryData<string> Endpoints =>
    [
        "list-jobs", "get-job", "create-job", "update-job", "list-paths", "add-path", "remove-path", "my-centre", "centre", "put-profile",
    ];

    private static readonly object[] NoRequirements = [];

    private Task<HttpResponseMessage> CallAsync(HttpClient client, string endpoint, Guid job, Guid path) => endpoint switch
    {
        "list-jobs" => client.GetAsync("/api/v1/career/jobs", Ct),
        "get-job" => client.GetAsync($"/api/v1/career/jobs/{job}", Ct),
        "create-job" => client.PostAsJsonAsync("/api/v1/career/jobs", new { code = $"SEC_{Guid.NewGuid():N}"[..20].ToUpperInvariant(), title = "T", level = 1, requirements = NoRequirements }, Ct),
        "update-job" => client.PutAsJsonAsync($"/api/v1/career/jobs/{job}", new { title = "T", level = 1, requirements = NoRequirements }, Ct),
        "list-paths" => client.GetAsync("/api/v1/career/paths", Ct),
        "add-path" => client.PostAsJsonAsync("/api/v1/career/paths", new { fromJobId = job, toJobId = Guid.NewGuid() }, Ct),
        "remove-path" => client.DeleteAsync($"/api/v1/career/paths/{path}", Ct),
        "my-centre" => client.GetAsync("/api/v1/career/me/centre", Ct),
        "centre" => client.GetAsync($"/api/v1/career/employees/{host.Employees[Employee]}/centre", Ct),
        "put-profile" => client.PutAsJsonAsync($"/api/v1/career/employees/{host.Employees[Employee]}/profile", new { competencies = NoRequirements }, Ct),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
    };

    private async Task<(Guid Job, Guid Path)> SeedAsync()
    {
        using var admin = await host.LoginAsync(Admin);
        async Task<Guid> NewJob(string title) => (await ReadAsync(await admin.PostAsJsonAsync("/api/v1/career/jobs",
            new { code = $"SEED_{Guid.NewGuid():N}"[..20].ToUpperInvariant(), title, level = 1, requirements = NoRequirements }, Ct))).GetProperty("id").GetGuid();
        var from = await NewJob("From");
        var to = await NewJob("To");
        var path = (await ReadAsync(await admin.PostAsJsonAsync("/api/v1/career/paths", new { fromJobId = from, toJobId = to }, Ct))).GetProperty("id").GetGuid();
        return (from, path);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutSession_Returns401(string endpoint)
    {
        var (job, path) = await SeedAsync();
        using var client = host.Anonymous();

        var response = await CallAsync(client, endpoint, job, path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("create-job")]
    [InlineData("update-job")]
    [InlineData("add-path")]
    [InlineData("remove-path")]
    [InlineData("put-profile")]
    public async Task Writes_AreForbiddenForEveryoneButHr(string endpoint)
    {
        var (job, path) = await SeedAsync();

        foreach (var user in new[] { Employee, Manager, Committee })
        {
            using var client = await host.LoginAsync(user);
            (await CallAsync(client, endpoint, job, path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"{endpoint} as {user}");
        }
    }

    [Theory]
    [InlineData("list-jobs", Employee)]
    [InlineData("list-jobs", Manager)]
    [InlineData("list-paths", Employee)]
    [InlineData("list-paths", Committee)]
    [InlineData("my-centre", Employee)]
    public async Task Reads_FollowTheMatrix(string endpoint, string user)
    {
        var (job, path) = await SeedAsync();
        using var client = await host.LoginAsync(user);

        (await CallAsync(client, endpoint, job, path)).StatusCode.ShouldBe(HttpStatusCode.OK, $"{endpoint} as {user}");
    }

    [Theory]
    [InlineData("create-job")]
    [InlineData("update-job")]
    [InlineData("add-path")]
    [InlineData("remove-path")]
    [InlineData("put-profile")]
    public async Task StateChangingCall_WithoutAntiforgeryHeader_Returns400(string endpoint)
    {
        var (job, path) = await SeedAsync();
        using var client = await host.LoginAsync(Admin, withAntiforgery: false);

        (await CallAsync(client, endpoint, job, path)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownJob_IsForbiddenForCallersWithNoRightsAtAll_SoIdsCannotBeProbed()
    {
        using var employee = await host.LoginAsync(Employee);

        // Employees may view jobs, so a missing job is a plain 404 for them; the write is a 403 before any lookup.
        (await employee.GetAsync($"/api/v1/career/jobs/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await employee.PutAsJsonAsync($"/api/v1/career/jobs/{Guid.NewGuid()}", new { title = "x", level = 1, requirements = NoRequirements }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
