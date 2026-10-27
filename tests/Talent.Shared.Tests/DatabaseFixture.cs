using Microsoft.EntityFrameworkCore;
using Talent.Shared.Persistence;
using Talent.TestSupport;

namespace Talent.Shared.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public string ConnectionString => _postgres.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync(async connection =>
        {
            await using var shared = NewSharedContext(connection);
            await shared.Database.MigrateAsync();
            await using var widgets = new WidgetDbContext(WidgetOptions(connection));
            await widgets.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS public.widgets (id uuid PRIMARY KEY, name text NOT NULL UNIQUE)");
        });
    }

    public async Task ResetAsync()
    {
        await _postgres.ResetAsync();
        await using var widgets = new WidgetDbContext(WidgetOptions(ConnectionString));
        await widgets.Database.ExecuteSqlRawAsync("DELETE FROM public.widgets");
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    public TalentSharedDbContext NewSharedContext(string? connection = null) =>
        new(new DbContextOptionsBuilder<TalentSharedDbContext>()
            .UseNpgsql(connection ?? ConnectionString, o => o.MigrationsHistoryTable("__ef_migrations", SharedModel.Schema))
            .Options);

    public WidgetDbContext NewWidgetContext()
    {
        return new WidgetDbContext(WidgetOptions(ConnectionString));
    }

    private static DbContextOptions<WidgetDbContext> WidgetOptions(string connection) =>
        new DbContextOptionsBuilder<WidgetDbContext>()
            .UseNpgsql(connection)
            .AddInterceptors(new Talent.Shared.Events.OutboxSaveChangesInterceptor())
            .Options;
}
