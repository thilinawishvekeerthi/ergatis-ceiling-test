using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Talent.TestSupport;

namespace Talent.Assessment.IntegrationTests;

/// <summary>
/// The real host on a real PostgreSQL database: migrations, the mock Core HR sync on boot (12 employees),
/// the default nine-box config and the default permission matrix, all through the startup path the demo uses.
/// </summary>
public sealed class NineBoxHostFixture : IAsyncLifetime
{
    public const string Reviewer = "test.reviewer";
    public const string Committee = "test.committee";
    public const string Employee = "test.employee";
    public const int SeededEmployees = 12;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly PostgresFixture _postgres = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>
    /// Evaluation time is controlled by the tests, so latest-wins can be checked with out-of-order saves.
    /// Starts at the real time because the session cookie's expiry is checked by the real test client.
    /// </summary>
    public ManualClock Clock { get; } = new(DateTimeOffset.UtcNow);

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public string ConnectionString => _postgres.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync(_ => Task.CompletedTask);
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Talent", _postgres.ConnectionString);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Authentication:Mode", "Fake");
            builder.UseSetting("Security:Cookie:RequireSecure", "false");
            builder.UseSetting("Integration:CORE_HR:Mode", "Mock");
            builder.UseSetting("Integration:CORE_HR:Mock:Employees", SeededEmployees.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Integration:Sync:SyncOnStartupWhenEmpty", "true");
            builder.UseSetting("Workflow:SlaTickEnabled", "false");
            Seed(builder, 0, Reviewer, "HR_ADMIN");
            Seed(builder, 1, Committee, "TALENT_COMMITTEE");
            Seed(builder, 2, Employee, "EMPLOYEE");
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
        });

        _ = Factory.Server; // starts the host, which migrates, syncs and seeds
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<HttpClient> LoginAsync(string subject)
    {
        var client = Factory.CreateClient();
        (await client.PostAsJsonAsync("/bff/fake-login", new { subject }, Ct)).EnsureSuccessStatusCode();
        var token = await client.GetFromJsonAsync<JsonElement>("/bff/antiforgery", Ct);
        client.DefaultRequestHeaders.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());
        return client;
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Ct), Web);

    /// <summary>The seeded employees in list order (by employee number).</summary>
    public static async Task<List<JsonElement>> EmployeesAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/assessment/nine-box/employees?pageSize=100", Ct);
        return [.. page.GetProperty("items").EnumerateArray()];
    }

    private static void Seed(IWebHostBuilder builder, int index, string subject, string role)
    {
        builder.UseSetting($"Security:Seed:{index}:Subject", subject);
        builder.UseSetting($"Security:Seed:{index}:EmployeeId", Guid.NewGuid().ToString());
        builder.UseSetting($"Security:Seed:{index}:Roles:0", role);
    }
}

/// <summary>A clock the test sets by hand. Unlike FakeTimeProvider it may go back, to simulate a late save of an older evaluation.</summary>
public sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private long _ticks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
}
