using Microsoft.EntityFrameworkCore;
using Talent.Shared.Events;
using Talent.Shared.Persistence;
using Talent.TestSupport;
using Talent.Workflow.Persistence;

namespace Talent.Workflow.IntegrationTests;

public sealed class WorkflowDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public string ConnectionString => _postgres.ConnectionString;

    public async ValueTask InitializeAsync() =>
        await _postgres.InitializeAsync(async connection =>
        {
            await using var shared = new TalentSharedDbContext(
                new DbContextOptionsBuilder<TalentSharedDbContext>()
                    .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations", SharedModel.Schema)).Options);
            await shared.Database.MigrateAsync();
            await using var workflow = WorkflowDbContextFactory.Create(connection, new OutboxSaveChangesInterceptor());
            await workflow.Database.MigrateAsync();
        });

    public Task ResetAsync() => _postgres.ResetAsync();

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();
}
