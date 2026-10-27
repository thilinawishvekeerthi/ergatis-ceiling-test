using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Scales;
using Talent.Config.Versions;
using Talent.Shared.Persistence;

namespace Talent.Config.Persistence;

internal sealed class ConfigDbContext(DbContextOptions<ConfigDbContext> options) : DbContext(options)
{
    public const string Schema = "config";

    public DbSet<ConfigDefinition> Definitions => Set<ConfigDefinition>();
    public DbSet<ConfigVersion> Versions => Set<ConfigVersion>();
    public DbSet<ScaleLevelRow> ScaleLevels => Set<ScaleLevelRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ConfigDefinition>(b =>
        {
            b.ToTable("config_definitions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => new { x.Type, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ConfigVersion>(b =>
        {
            b.ToTable("config_versions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.Checksum).HasMaxLength(64);
            b.Property(x => x.PublishedBy).HasMaxLength(200);
            b.Ignore(x => x.DomainEvents);
            b.HasOne<ConfigDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.DefinitionId, x.VersionNo }).IsUnique();
            // Optimistic concurrency with PostgreSQL's xmin system column (ADR-02, postgres-data-model skill).
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<ScaleLevelRow>(b =>
        {
            b.ToTable("scale_levels");
            b.HasKey(x => new { x.VersionId, x.Level });
            b.Property(x => x.Label).HasMaxLength(100);
            b.Property(x => x.Band).HasMaxLength(50);
            b.Property(x => x.MinScore).HasPrecision(9, 3);
            b.HasOne<ConfigVersion>().WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.Band);
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
