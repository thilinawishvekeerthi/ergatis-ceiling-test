using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Talent.Shared;
using Talent.Shared.Events;
using Talent.Shared.Persistence;
using Talent.Shared.Security;
using Talent.Workflow.Contracts;
using Talent.Workflow.Defaults;
using Talent.Workflow.Persistence;

namespace Talent.Workflow.IntegrationTests;

/// <summary>The Workflow module wired like the host, on the real database, with a fake clock and fake people lookups.</summary>
internal sealed class WorkflowHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly TestUser _user;

    private WorkflowHarness(ServiceProvider provider, FakeTimeProvider clock, FakeDirectory directory, TestUser user)
    {
        _provider = provider;
        _user = user;
        Clock = clock;
        Directory = directory;
    }

    public FakeTimeProvider Clock { get; }

    public FakeDirectory Directory { get; }

    public IServiceProvider Services => _provider;

    public static async Task<WorkflowHarness> CreateAsync(
        WorkflowDatabaseFixture db, bool tickEnabled = false, Action<IServiceCollection>? configure = null)
    {
        await db.ResetAsync();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Talent"] = db.ConnectionString,
            ["Workflow:SlaTickEnabled"] = tickEnabled.ToString(),
            ["Workflow:SlaTickSeconds"] = "60",
        }).Build();

        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero));
        var directory = new FakeDirectory();
        var user = new TestUser();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddFilter(_ => false));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddTalentShared(configuration);
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<IManagerLookup>(directory);
        services.AddSingleton<IRoleMemberLookup>(directory);
        configure?.Invoke(services);
        services.AddWorkflowModule(configuration);

        var provider = services.BuildServiceProvider();
        var harness = new WorkflowHarness(provider, clock, directory, user);
        await using (var scope = provider.CreateAsyncScope())
        {
            await WorkflowDefaultsSeeder.EnsureAsync(
                scope.ServiceProvider.GetRequiredService<WorkflowDbContext>(), clock, CancellationToken.None);
        }

        return harness;
    }

    /// <summary>Makes the next calls run as this user.</summary>
    public WorkflowHarness As(string userId)
    {
        _user.UserId = userId;
        return this;
    }

    /// <summary>A service in a fresh scope, the way each request gets one.</summary>
    public async Task<T> RunAsync<T>(Func<IWorkflowService, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IWorkflowService>());
    }

    public async Task RunAsync(Func<IWorkflowService, Task> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IWorkflowService>());
    }

    public async Task<TResult> WithAsync<TService, TResult>(Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public Task<int> RunSlaTickAsync() =>
        _provider.GetRequiredService<SlaTickRunner>().RunOnceAsync(CancellationToken.None);

    public async Task<int> DispatchOutboxAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchPendingAsync(100, CancellationToken.None);
    }

    public async Task<IReadOnlyList<string>> OutboxEventTypesAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var shared = scope.ServiceProvider.GetRequiredService<TalentSharedDbContext>();
        var types = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            shared.OutboxMessages.OrderBy(m => m.OccurredAt).ThenBy(m => m.Id).Select(m => m.EventType));
        return [.. types.Select(t => t.Split(',')[0].Split('.')[^1])];
    }

    public IHostedService TickService() =>
        _provider.GetServices<IHostedService>().Single(s => s.GetType().Name == "SlaTickService");

    public async Task<Guid> StartIdpAsync(string employee = "emp-1", string subject = "plan-1", string? cost = null) =>
        await As(employee).RunAsync(s => s.StartAsync(new StartWorkflowRequest(
            WorkflowCodes.IdpApproval, "Idp", subject, employee,
            cost is null ? null : new Dictionary<string, string> { ["estimatedCost"] = cost }), CancellationToken.None));

    public async Task ActAsync(string user, Guid id, string action, string? comment = null) =>
        await As(user).RunAsync(s => s.TransitionAsync(id, action, comment, CancellationToken.None));

    public Task<WorkflowInstanceDto> GetAsync(Guid id) => RunAsync(s => s.GetAsync(id, CancellationToken.None));

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    public sealed class TestUser : ICurrentUser
    {
        public bool IsAuthenticated => true;

        private static readonly AsyncLocal<string?> Current = new();

        /// <summary>Per async flow, so two tasks can act as two users at the same time.</summary>
        public string UserId
        {
            get => Current.Value ?? "emp-1";
            set => Current.Value = value;
        }
    }

    public sealed class FakeDirectory : IManagerLookup, IRoleMemberLookup
    {
        public Dictionary<string, string> Managers { get; } = new() { ["emp-1"] = "mgr-1" };

        public Dictionary<string, string[]> Roles { get; } = new()
        {
            ["HR_ADMIN"] = ["hr-1"],
            ["BOARD_CHAIR"] = ["chair-1"],
        };

        public Task<string?> GetManagerUserIdAsync(string employeeId, CancellationToken cancellationToken) =>
            Task.FromResult(Managers.GetValueOrDefault(employeeId));

        public Task<IReadOnlyList<string>> GetUserIdsAsync(string roleCode, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Roles.GetValueOrDefault(roleCode) ?? []);
    }
}
