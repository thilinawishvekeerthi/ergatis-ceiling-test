using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Talent.Integration.Contracts;
using Talent.Notification.Email;
using Talent.Notification.Persistence;
using Talent.Security.Persistence;
using Talent.Shared.Auditing;
using Talent.Shared.Events;
using Talent.Shared.Persistence;
using Talent.TestSupport;

namespace Talent.Notification.IntegrationTests;

/// <summary>
/// The real host on a real PostgreSQL database with the fake login, a recording email sender and a fake HR employee source
/// (so each user has an email address). The background workers are off, so tests decide when events and mail are processed.
/// </summary>
public class NotificationHostFixture : IAsyncLifetime
{
    public const string Admin = "hr.admin";
    public const string Manager = "line.manager";
    public const string OtherManager = "other.manager";
    public const string Employee = "plain.employee";
    public const string NoMail = "no.mail";
    public const string Committee = "committee.member";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly bool _backgroundWorkers;
    private readonly PostgresFixture _postgres = new();
    private readonly FakeEmployees _employees = new();

    public NotificationHostFixture()
        : this(backgroundWorkers: false)
    {
    }

    protected NotificationHostFixture(bool backgroundWorkers) => _backgroundWorkers = backgroundWorkers;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public Dictionary<string, Guid> Ids { get; } = [];

    public RecordingEmailSender Mail { get; } = new();

    public string ConnectionString => _postgres.ConnectionString;

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string EmailOf(string user) => $"{user}@example.com";

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
            builder.UseSetting("Outbox:DispatchIntervalSeconds", _backgroundWorkers ? "1" : "0");
            builder.UseSetting("Notification:Email:PollSeconds", _backgroundWorkers ? "1" : "0");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Mail);
                services.RemoveAll<IEmployeeReader>();
                services.AddSingleton<IEmployeeReader>(_employees);
            });
        });
        _ = Factory.Server;

        await AddUserAsync(Admin, "HR_ADMIN");
        await AddUserAsync(Manager, "MANAGER");
        await AddUserAsync(OtherManager, "MANAGER");
        await AddUserAsync(Employee, "EMPLOYEE");
        await AddUserAsync(NoMail, "EMPLOYEE", withEmail: false);
        await AddUserAsync(Committee, "TALENT_COMMITTEE");
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
        GC.SuppressFinalize(this);
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

    /// <summary>Runs the outbox dispatcher until nothing is pending, as the background worker would.</summary>
    public async Task DispatchEventsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        while (await dispatcher.DispatchPendingAsync(50, Ct) > 0)
        {
        }
    }

    public async Task<int> SendMailAsync()
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<EmailDeliveryService>().SendPendingAsync(50, Ct);
    }

    internal NotificationDbContext NewContext() => NotificationDbContextFactory.Create(_postgres.ConnectionString, new OutboxSaveChangesInterceptor());

    public int CountAudit(string action, string actor)
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TalentSharedDbContext>().Set<AuditEntry>().Count(a => a.Action == action && a.ActorId == actor);
    }

    private async Task AddUserAsync(string subject, string role, bool withEmail = true)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        var employee = Guid.NewGuid();
        Ids[subject] = employee;
        db.Accounts.Add(new UserAccount(subject, employee));
        db.RoleAssignments.Add(new RoleAssignment(subject, role));
        await db.SaveChangesAsync(Ct);
        _employees.Add(new EmployeeDto(employee, $"E-{subject}", subject, "User", withEmail ? EmailOf(subject) : null, null, null, null, null, "Active"));
    }
}

/// <summary>The same host with the outbox dispatcher and the mail worker running, to prove events flow without anyone calling them.</summary>
public sealed class BackgroundNotificationHostFixture() : NotificationHostFixture(backgroundWorkers: true);

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly List<(string To, string Subject, string Body)> _sent = [];
    private readonly Lock _gate = new();

    public Exception? FailWith { get; set; }

    public IReadOnlyList<(string To, string Subject, string Body)> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (FailWith is { } failure)
        {
            throw failure;
        }

        lock (_gate)
        {
            _sent.Add((to, subject, body));
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeEmployees : IEmployeeReader
{
    private readonly Dictionary<Guid, EmployeeDto> _byId = [];

    public void Add(EmployeeDto employee) => _byId[employee.Id] = employee;

    public Task<EmployeeDto?> GetAsync(Guid employeeId, CancellationToken cancellationToken) => Task.FromResult(_byId.GetValueOrDefault(employeeId));

    public Task<EmployeeDto?> FindByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken) =>
        Task.FromResult(_byId.Values.FirstOrDefault(e => e.EmployeeNumber == employeeNumber));

    public Task<IReadOnlyList<EmployeeDto>> GetManyAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeDto>>([.. employeeIds.Where(_byId.ContainsKey).Select(i => _byId[i])]);

    public Task<IReadOnlyList<EmployeeDto>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeDto>>([.. _byId.Values.Where(e => e.ManagerId == managerId)]);

    public Task<PagedResult<EmployeeDto>> ListAsync(Guid? orgUnitId, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult(new PagedResult<EmployeeDto>([.. _byId.Values], page, pageSize, _byId.Count));
}
