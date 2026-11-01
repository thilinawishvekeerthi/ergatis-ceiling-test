using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Talent.Security.Persistence;
using Talent.Shared.Auditing;
using Talent.Shared.Persistence;
using Talent.TestSupport;

namespace Talent.Career.IntegrationTests;

/// <summary>The real host on a real PostgreSQL database with the fake login. One user per role, each linked to an employee.</summary>
public sealed class CareerHostFixture : IAsyncLifetime
{
    public const string Admin = "hr.admin";
    public const string Manager = "line.manager";
    public const string Employee = "plain.employee";
    public const string OtherEmployee = "other.employee";
    public const string Committee = "committee.member";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly PostgresFixture _postgres = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public Dictionary<string, Guid> Employees { get; } = [];

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

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
        await AddUserAsync(OtherEmployee, "EMPLOYEE");
        await AddUserAsync(Committee, "TALENT_COMMITTEE");
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient Anonymous() => Factory.CreateClient();

    public async Task<HttpClient> LoginAsync(string subject, bool withAntiforgery = true)
    {
        var client = Factory.CreateClient();
        (await client.PostAsJsonAsync("/bff/fake-login", new { subject }, Ct)).EnsureSuccessStatusCode();
        if (withAntiforgery)
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/bff/antiforgery", Ct);
            client.DefaultRequestHeaders.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("requestToken").GetString());
        }

        return client;
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Ct), Web);

    public int CountAudit(string action, string actor, bool confidential)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TalentSharedDbContext>();
        return db.Set<AuditEntry>().Count(a => a.Action == action && a.ActorId == actor && a.Confidential == confidential);
    }

    private async Task AddUserAsync(string subject, string role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        var employee = Guid.NewGuid();
        Employees[subject] = employee;
        db.Accounts.Add(new UserAccount(subject, employee));
        db.RoleAssignments.Add(new RoleAssignment(subject, role));
        await db.SaveChangesAsync(Ct);
    }
}
