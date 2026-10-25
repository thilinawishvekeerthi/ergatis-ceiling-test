using Microsoft.EntityFrameworkCore;
using Talent.Integration.Canonical;
using Talent.Shared.Persistence;

namespace Talent.Integration.Persistence;

internal sealed class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : DbContext(options)
{
    public const string Schema = "integration";

    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<ExternalIdMapEntry> IdMap => Set<ExternalIdMapEntry>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
    public DbSet<DeadLetter> DeadLetters => Set<DeadLetter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OrgUnit>(b =>
        {
            b.ToTable("org_units");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.ParentId);
        });

        modelBuilder.Entity<Position>(b =>
        {
            b.ToTable("positions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Title).HasMaxLength(200);
            b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.OrgUnitId);
        });

        modelBuilder.Entity<Employee>(b =>
        {
            b.ToTable("employees");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EmployeeNumber).HasMaxLength(50);
            b.Property(x => x.FirstName).HasMaxLength(200);
            b.Property(x => x.LastName).HasMaxLength(200);
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.Status).HasMaxLength(20);
            b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.EmployeeNumber).IsUnique();
            b.HasIndex(x => x.OrgUnitId);
            b.HasIndex(x => x.PositionId);
            b.HasIndex(x => x.ManagerId);
        });

        modelBuilder.Entity<ExternalIdMapEntry>(b =>
        {
            b.ToTable("external_id_map");
            b.HasKey(x => new { x.System, x.Entity, x.ExternalId });
            b.Property(x => x.System).HasMaxLength(30);
            b.Property(x => x.Entity).HasMaxLength(40);
            b.Property(x => x.ExternalId).HasMaxLength(100);
            b.HasIndex(x => new { x.Entity, x.InternalId });
        });

        modelBuilder.Entity<SyncState>(b =>
        {
            b.ToTable("sync_state");
            b.HasKey(x => new { x.System, x.Entity });
            b.Property(x => x.System).HasMaxLength(30);
            b.Property(x => x.Entity).HasMaxLength(40);
        });

        modelBuilder.Entity<DeadLetter>(b =>
        {
            b.ToTable("dead_letters");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.System).HasMaxLength(30);
            b.Property(x => x.Entity).HasMaxLength(40);
            b.Property(x => x.ExternalId).HasMaxLength(100);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.Error).HasMaxLength(1000);
            // One open dead letter per record. A new failure bumps Attempts instead of adding a row.
            b.HasIndex(x => new { x.System, x.Entity, x.ExternalId }).IsUnique().HasFilter("resolved_at IS NULL")
                .HasDatabaseName("ux_dead_letters_open");
            b.HasIndex(x => x.LastFailedAt);
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
