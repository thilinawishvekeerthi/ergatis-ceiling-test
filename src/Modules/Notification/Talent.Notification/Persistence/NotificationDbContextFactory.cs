using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Talent.Shared.Events;

namespace Talent.Notification.Persistence;

/// <summary>Builds the context for tests and for <c>dotnet ef</c> (reads TALENT_DB, else the local dev database).</summary>
internal sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) =>
        Create(
            Environment.GetEnvironmentVariable("TALENT_DB") ?? "Host=127.0.0.1;Port=5433;Username=postgres;Database=talent",
            new OutboxSaveChangesInterceptor());

    public static NotificationDbContext Create(string connectionString, params IInterceptor[] interceptors) =>
        new(Configure(new DbContextOptionsBuilder<NotificationDbContext>(), connectionString).AddInterceptors(interceptors).Options);

    public static DbContextOptionsBuilder<NotificationDbContext> Configure(
        DbContextOptionsBuilder<NotificationDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations", NotificationDbContext.Schema));
}
