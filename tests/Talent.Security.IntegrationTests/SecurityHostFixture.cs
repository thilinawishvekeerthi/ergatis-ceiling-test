using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Talent.Security.Persistence;
using Talent.TestSupport;

namespace Talent.Security.IntegrationTests;

/// <summary>
/// The real host on a real PostgreSQL database, with the fake login handler turned on.
/// Users are rows in the TalentManaged role tables, so roles come through the same path as in production.
/// </summary>
public sealed class SecurityHostFixture : IAsyncLifetime
{
    public const string Admin = "hr.admin";
    public const string Manager = "line.manager";
    public const string Employee = "plain.employee";
    public const string Committee = "committee.member";
    public const string ScopedAdmin = "scoped.admin";
    public const string NoEmployee = "ghost";
    public const string Nobody = "nobody";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly PostgresFixture _postgres = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public Dictionary<string, Guid> Employees { get; } = [];

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync(_ => Task.CompletedTask);
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Talent", _postgres.ConnectionString);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Authentication:Mode", "Fake");
            builder.UseSetting("Security:Cookie:RequireSecure", "false");
            builder.UseSetting("Security:RoleSource", "TalentManaged");
        });

        _ = Factory.Server; // starts the host, which migrates and seeds the default matrix

        await AddUserAsync(Admin, "HR_ADMIN");
        await AddUserAsync(Manager, "MANAGER");
        await AddUserAsync(Employee, "EMPLOYEE");
        await AddUserAsync(Committee, "TALENT_COMMITTEE");
        await AddUserAsync(ScopedAdmin, "SCOPED_ADMIN");
        await AddUserAsync(Nobody);
        await AddUserAsync(NoEmployee, "HR_ADMIN", withEmployee: false);

        await PublishExtraRolesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    /// <summary>A client signed in through the fake login handler, with the anti-forgery header ready for state-changing calls.</summary>
    public async Task<HttpClient> LoginAsync(string subject, bool withAntiforgery = true)
    {
        var client = Factory.CreateClient();
        var login = await client.PostAsJsonAsync("/bff/fake-login", new { subject }, Ct);
        login.EnsureSuccessStatusCode();

        if (withAntiforgery)
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/bff/antiforgery", Ct);
            client.DefaultRequestHeaders.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());
        }

        return client;
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string NewCode() => $"T_{Guid.NewGuid():N}"[..20].ToUpperInvariant();

    public static string ScalePayload { get; } =
        """{"levels":[{"level":1,"label":"Low","minScore":1,"band":"LOW"},{"level":2,"label":"High","minScore":2,"band":"HIGH"}]}""";

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Ct), Web);

    /// <summary>Creates a scale as the admin and returns its first draft version id.</summary>
    public static async Task<(string Code, Guid VersionId)> CreateScaleDraftAsync(HttpClient admin)
    {
        var code = NewCode();
        var response = await admin.PostAsJsonAsync($"/api/v1/config/Scale/{code}/drafts", new { name = "Test scale", payload = JsonDocument.Parse(ScalePayload).RootElement }, Ct);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
        return (code, (await ReadAsync(response)).GetProperty("id").GetGuid());
    }

    public static async Task<(string Code, Guid VersionId)> CreatePublishedScaleAsync(HttpClient admin)
    {
        var (code, id) = await CreateScaleDraftAsync(admin);
        (await admin.PostAsync($"/api/v1/config/versions/{id}/publish", null, Ct)).EnsureSuccessStatusCode();
        return (code, id);
    }

    internal async Task<int> CountAuditAsync(string action, string actor)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Talent.Shared.Persistence.TalentSharedDbContext>();
        return db.Set<Talent.Shared.Auditing.AuditEntry>().Count(a => a.Action == action && a.ActorId == actor);
    }

    private async Task AddUserAsync(string subject, string roles = "", bool withEmployee = true)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        if (withEmployee)
        {
            var employee = Guid.NewGuid();
            Employees[subject] = employee;
            db.Accounts.Add(new UserAccount(subject, employee));
        }

        foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            db.RoleAssignments.Add(new RoleAssignment(subject, role));
        }

        await db.SaveChangesAsync(Ct);
    }

    /// <summary>Publishes matrix version 2 with an extra role whose scope is DirectReports, to test out-of-scope.</summary>
    private async Task PublishExtraRolesAsync()
    {
        using var admin = await LoginAsync(Admin);
        var current = await admin.GetFromJsonAsync<JsonElement>("/api/v1/config/PermissionMatrix/DEFAULT/latest", Ct);
        var grants = JsonSerializer.Deserialize<List<JsonElement>>(current.GetProperty("payload").GetProperty("grants").GetRawText(), Web)!;
        var extra = JsonDocument.Parse(
            """{"role":"SCOPED_ADMIN","resource":"config:*","actions":["View","Edit","Approve"],"scope":"DirectReports","fieldClass":"Internal","talentConfidential":false}""").RootElement;
        var payload = new { grants = grants.Append(extra) };

        var draft = await admin.PostAsJsonAsync("/api/v1/config/PermissionMatrix/DEFAULT/drafts", new { payload }, Ct);
        draft.EnsureSuccessStatusCode();
        var id = (await ReadAsync(draft)).GetProperty("id").GetGuid();
        (await admin.PostAsync($"/api/v1/config/versions/{id}/publish", null, Ct)).EnsureSuccessStatusCode();
    }
}
