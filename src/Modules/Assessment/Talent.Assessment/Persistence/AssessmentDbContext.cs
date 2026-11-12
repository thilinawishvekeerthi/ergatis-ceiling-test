using Microsoft.EntityFrameworkCore;
using Talent.Assessment.NineBox;
using Talent.Shared.Persistence;

namespace Talent.Assessment.Persistence;

internal sealed class AssessmentDbContext(DbContextOptions<AssessmentDbContext> options) : DbContext(options)
{
    public const string Schema = "assessment";

    public DbSet<NineBoxEvaluation> Evaluations => Set<NineBoxEvaluation>();
    public DbSet<LatestNineBoxPlacement> LatestPlacements => Set<LatestNineBoxPlacement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<NineBoxEvaluation>(b =>
        {
            b.ToTable("nine_box_evaluations", t =>
            {
                // The database rejects a row that breaks the BRD rules, whatever code wrote it.
                t.HasCheckConstraint("ck_nine_box_evaluations_performance", "performance_level BETWEEN 1 AND 3");
                t.HasCheckConstraint("ck_nine_box_evaluations_potential", "potential_level BETWEEN 1 AND 3");
                t.HasCheckConstraint("ck_nine_box_evaluations_box", "box_number = (potential_level - 1) * 3 + performance_level");
                t.HasCheckConstraint("ck_nine_box_evaluations_score", "talent_score = performance_level * potential_level");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.PerformanceLevel).HasColumnType("smallint");
            b.Property(x => x.PotentialLevel).HasColumnType("smallint");
            b.Property(x => x.BoxNumber).HasColumnType("smallint");
            b.Property(x => x.TalentScore).HasColumnType("smallint");
            b.Property(x => x.BoxLabel).HasMaxLength(100);
            b.Property(x => x.Band).HasMaxLength(20);
            b.Property(x => x.ReviewerId).HasMaxLength(200);
            b.Ignore(x => x.DomainEvents);
            // Target of the composite foreign key below: a latest pointer can only name an evaluation of the same employee.
            b.HasAlternateKey(x => new { x.Id, x.EmployeeId });
            // Serves the history list: one employee, newest first.
            b.HasIndex(x => new { x.EmployeeId, x.EvaluatedAt }).IsDescending(false, true);
        });

        modelBuilder.Entity<LatestNineBoxPlacement>(b =>
        {
            b.ToTable("latest_nine_box_placements");
            b.HasKey(x => x.EmployeeId);
            b.Property(x => x.EmployeeId).ValueGeneratedNever();
            b.HasOne<NineBoxEvaluation>().WithOne()
                .HasForeignKey<LatestNineBoxPlacement>(x => new { x.EvaluationId, x.EmployeeId })
                .HasPrincipalKey<NineBoxEvaluation>(x => new { x.Id, x.EmployeeId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
