using Microsoft.AspNetCore.Mvc.Testing;
using Talent.TestSupport;

namespace Talent.Host.Tests;

/// <summary>The real host on a real, empty PostgreSQL database. The host migrates and seeds it at startup.</summary>
public sealed class HostFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync(_ => Task.CompletedTask);
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Talent", _postgres.ConnectionString);
            builder.UseSetting("Database:MigrateOnStartup", "true");
        });
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
