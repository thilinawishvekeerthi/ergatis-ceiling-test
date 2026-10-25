using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Talent.Config.Persistence;
using Talent.Shared.Events;
using Talent.Shared.Persistence;
using Talent.Shared.Security;
using Talent.TestSupport;

namespace Talent.Config.IntegrationTests;

public sealed class ConfigDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync() =>
        await _postgres.InitializeAsync(async connection =>
        {
            await using var shared = new TalentSharedDbContext(
                new DbContextOptionsBuilder<TalentSharedDbContext>()
                    .UseNpgsql(connection, o => o.MigrationsHistoryTable("__ef_migrations", SharedModel.Schema)).Options);
            await shared.Database.MigrateAsync();
            await using var config = ConfigDbContextFactory.Create(connection, new OutboxSaveChangesInterceptor());
            await config.Database.MigrateAsync();
        });

    public Task ResetAsync() => _postgres.ResetAsync();

    public ValueTask DisposeAsync() => _postgres.DisposeAsync();

    internal ConfigDbContext NewContext() =>
        ConfigDbContextFactory.Create(_postgres.ConnectionString, new OutboxSaveChangesInterceptor());

    public TalentSharedDbContext NewSharedContext() =>
        new(new DbContextOptionsBuilder<TalentSharedDbContext>()
            .UseNpgsql(_postgres.ConnectionString, o => o.MigrationsHistoryTable("__ef_migrations", SharedModel.Schema)).Options);

    internal ConfigAdminService NewAdmin(ConfigDbContext context, string userId = "hr.admin") =>
        new(context, new TestUser(userId), Clock);

    public sealed class TestUser(string userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public string UserId => userId;
    }
}
