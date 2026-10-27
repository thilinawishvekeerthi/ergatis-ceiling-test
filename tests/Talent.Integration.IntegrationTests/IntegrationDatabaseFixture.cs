using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Talent.Integration.Adapters;
using Talent.Integration.Contracts;
using Talent.Integration.Persistence;
using Talent.Integration.Readers;
using Talent.Integration.Sync;
using Talent.Shared.Events;
using Talent.Shared.Persistence;
using Talent.TestSupport;

namespace Talent.Integration.IntegrationTests;

public sealed class IntegrationDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    private static readonly DateTimeOffset ClockStart = new(2026, 3, 1, 1, 0, 0, TimeSpan.Zero);

    /// <summary>Replaced by <see cref="ResetAsync"/>, so every test starts at the same time whatever ran before it.</summary>
    public FakeTimeProvider Clock { get; private set; } = new(ClockStart);

    public string ConnectionString => _postgres.ConnectionString;

    public async ValueTask InitializeAsync() =>
        await _postgres.InitializeAsync(async connection =>
        {
            await using var shared = new TalentSharedDbContext(
                new DbContextOptionsBuilder<TalentSharedDbContext>()
                    .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations", SharedModel.Schema)).Options);
            await shared.Database.MigrateAsync();
            await using var integration = IntegrationDbContextFactory.Create(connection, new OutboxSaveChangesInterceptor());
            await integration.Database.MigrateAsync();
        });

    public async Task ResetAsync()
    {
        await _postgres.ResetAsync();
        Clock = new FakeTimeProvider(ClockStart);
    }

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    internal IntegrationDbContext NewContext() =>
        IntegrationDbContextFactory.Create(_postgres.ConnectionString, new OutboxSaveChangesInterceptor());

    internal SyncService NewSync(
        IntegrationDbContext context,
        ISourceAdapter<OrgUnitSource> orgs,
        ISourceAdapter<PositionSource> positions,
        ISourceAdapter<EmployeeSource> employees) =>
        new(context, orgs, positions, employees, Clock, NullLogger<SyncService>.Instance);

    internal SyncService NewSync(IntegrationDbContext context, MockSourceData data) =>
        NewSync(context, new MockOrgUnitAdapter(data), new MockPositionAdapter(data), new MockEmployeeAdapter(data));

    internal static EmployeeReader NewEmployeeReader(IntegrationDbContext context) => new(context);

    internal static OrgUnitReader NewOrgUnitReader(IntegrationDbContext context) => new(context);

    internal static PositionReader NewPositionReader(IntegrationDbContext context) => new(context);

    internal static DeadLetterReader NewDeadLetterReader(IntegrationDbContext context) => new(context);
}
