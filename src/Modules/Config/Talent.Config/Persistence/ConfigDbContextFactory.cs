using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Talent.Shared.Events;

namespace Talent.Config.Persistence;

/// <summary>Builds the context for tests and for <c>dotnet ef</c> (reads TALENT_DB, else the local dev database).</summary>
internal sealed class ConfigDbContextFactory : IDesignTimeDbContextFactory<ConfigDbContext>
{
    public ConfigDbContext CreateDbContext(string[] args) =>
        Create(
            Environment.GetEnvironmentVariable("TALENT_DB") ?? "Host=127.0.0.1;Port=5433;Username=postgres;Database=talent",
            new OutboxSaveChangesInterceptor());

    public static ConfigDbContext Create(string connectionString, params IInterceptor[] interceptors) =>
        new(Configure(new DbContextOptionsBuilder<ConfigDbContext>(), connectionString).AddInterceptors(interceptors).Options);

    public static DbContextOptionsBuilder<ConfigDbContext> Configure(
        DbContextOptionsBuilder<ConfigDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations", ConfigDbContext.Schema));
}
