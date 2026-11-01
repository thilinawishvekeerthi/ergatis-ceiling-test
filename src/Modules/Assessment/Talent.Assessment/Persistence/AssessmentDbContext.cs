using Microsoft.EntityFrameworkCore;
using Talent.Assessment.Cycles;
using Talent.Assessment.Reviews;
using Talent.Shared.Persistence;

namespace Talent.Assessment.Persistence;

internal sealed class AssessmentDbContext(DbContextOptions<AssessmentDbContext> options) : DbContext(options)
{
    public const string Schema = "assessment";

    public DbSet<ReviewCycle> Cycles => Set<ReviewCycle>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ReviewCycle>(b =>
        {
            b.ToTable("review_cycles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Ignore(x => x.DomainEvents);
            b.HasIndex(x => x.Name).IsUnique();
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<Review>(b =>
        {
            b.ToTable("reviews");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Comment).HasMaxLength(2000);
            b.Property(x => x.CalibratedBy).HasMaxLength(200);
            b.Property(x => x.CalibrationReason).HasMaxLength(2000);
            b.Ignore(x => x.DomainEvents);
            b.Ignore(x => x.FinalCell);
            b.Ignore(x => x.IsCalibrated);
            b.HasOne<ReviewCycle>().WithMany().HasForeignKey(x => x.CycleId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.CycleId, x.EmployeeId }).IsUnique();
            b.HasIndex(x => new { x.CycleId, x.ReviewerId });
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
