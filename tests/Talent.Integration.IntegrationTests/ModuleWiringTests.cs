using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Talent.Integration.Contracts;
using Talent.Shared;

namespace Talent.Integration.IntegrationTests;

public sealed class ModuleWiringTests(IntegrationDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value)
                .Concat([new KeyValuePair<string, string?>("ConnectionStrings:Talent", db.ConnectionString)]))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTalentShared(config).AddIntegrationModule(config);
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task Initialize_MigratesAndExposesContractsFromDi()
    {
        await using var provider = Build(("Integration:CORE_HR:Mock:Employees", "12"));
        await provider.InitializeIntegrationAsync(Ct);

        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISyncService>().RunDeltaAsync(Ct);

        result.Entities.Single(e => e.Entity == "Employee").Upserted.ShouldBe(12);
        (await scope.ServiceProvider.GetRequiredService<IEmployeeReader>().ListAsync(null, 1, 50, Ct)).TotalCount.ShouldBe(12);
        (await scope.ServiceProvider.GetRequiredService<IDeadLetterReader>().ListOpenAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Initialize_WhenSchedulerIsHangfire_RegistersNightlyJobInPostgresStorage()
    {
        await using var provider = Build(("Integration:Sync:Scheduler", "Hangfire"), ("Integration:Sync:Cron", "0 2 * * *"));
        await provider.InitializeIntegrationAsync(Ct);

        var job = provider.GetRequiredService<JobStorage>().GetConnection().GetRecurringJobs().Single(j => j.Id == "integration-nightly-sync");
        job.Cron.ShouldBe("0 2 * * *");
    }

    [Fact]
    public async Task Initialize_WhenSchedulerIsDisabled_RegistersNoJob()
    {
        await using var provider = Build();
        await provider.InitializeIntegrationAsync(Ct);

        provider.GetService<JobStorage>().ShouldBeNull();
    }

    [Fact]
    public void Build_WhenModeIsNotImplemented_FailsAtStartup()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Build(("Integration:CORE_HR:Mode", "Rest")));
        ex.Message.ShouldContain("Rest");
    }

    [Fact]
    public async Task Build_WhenHangfireCronIsEmpty_FailsOptionsValidation()
    {
        await using var provider = Build(("Integration:Sync:Scheduler", "Hangfire"), ("Integration:Sync:Cron", ""));

        await Should.ThrowAsync<Microsoft.Extensions.Options.OptionsValidationException>(
            () => provider.InitializeIntegrationAsync(Ct));
        _ = db;
    }
}
