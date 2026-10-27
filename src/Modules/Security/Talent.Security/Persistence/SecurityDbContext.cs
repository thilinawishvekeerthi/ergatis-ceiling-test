using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Talent.Shared.Persistence;

namespace Talent.Security.Persistence;

internal sealed class UserAccount(string subject, Guid employeeId)
{
    public string Subject { get; } = subject;

    public Guid EmployeeId { get; } = employeeId;
}

internal sealed class RoleAssignment(string subject, string role)
{
    public string Subject { get; } = subject;

    public string Role { get; } = role;
}

internal sealed class SecurityDbContext(DbContextOptions<SecurityDbContext> options) : DbContext(options)
{
    public const string Schema = "security";

    public DbSet<UserAccount> Accounts => Set<UserAccount>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<UserAccount>(b =>
        {
            b.ToTable("user_accounts");
            b.HasKey(x => x.Subject);
            b.Property(x => x.Subject).HasMaxLength(200);
            b.HasIndex(x => x.EmployeeId).IsUnique();
        });

        modelBuilder.Entity<RoleAssignment>(b =>
        {
            b.ToTable("role_assignments");
            b.HasKey(x => new { x.Subject, x.Role });
            b.Property(x => x.Subject).HasMaxLength(200);
            b.Property(x => x.Role).HasMaxLength(50);
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}

/// <summary>Builds the context for <c>dotnet ef</c> (reads TALENT_DB, else the local dev database).</summary>
internal sealed class SecurityDbContextFactory : IDesignTimeDbContextFactory<SecurityDbContext>
{
    public SecurityDbContext CreateDbContext(string[] args) =>
        new(Configure(
            new DbContextOptionsBuilder<SecurityDbContext>(),
            Environment.GetEnvironmentVariable("TALENT_DB") ?? "Host=127.0.0.1;Port=5433;Username=postgres;Database=talent").Options);

    public static DbContextOptionsBuilder<SecurityDbContext> Configure(
        DbContextOptionsBuilder<SecurityDbContext> builder, string connectionString) =>
        builder.UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__ef_migrations", SecurityDbContext.Schema));
}
