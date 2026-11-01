using Microsoft.EntityFrameworkCore;
using Talent.Career.Jobs;
using Talent.Career.Paths;
using Talent.Career.Profiles;
using Talent.Shared.Persistence;

namespace Talent.Career.Persistence;

internal sealed class CareerDbContext(DbContextOptions<CareerDbContext> options) : DbContext(options)
{
    public const string Schema = "career";

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobRequirement> JobRequirements => Set<JobRequirement>();
    public DbSet<CareerPath> Paths => Set<CareerPath>();
    public DbSet<EmployeeProfile> Profiles => Set<EmployeeProfile>();
    public DbSet<EmployeeCompetency> Competencies => Set<EmployeeCompetency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Job>(b =>
        {
            b.ToTable("jobs");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Title).HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasMany(x => x.Requirements).WithOne().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Requirements).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<JobRequirement>(b =>
        {
            b.ToTable("job_requirements");
            b.HasKey(x => new { x.JobId, x.CompetencyCode });
            b.Property(x => x.CompetencyCode).HasMaxLength(50);
            b.HasIndex(x => x.CompetencyCode);
        });

        modelBuilder.Entity<CareerPath>(b =>
        {
            b.ToTable("career_paths");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne<Job>().WithMany().HasForeignKey(x => x.FromJobId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Job>().WithMany().HasForeignKey(x => x.ToJobId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FromJobId, x.ToJobId }).IsUnique();
        });

        modelBuilder.Entity<EmployeeProfile>(b =>
        {
            b.ToTable("employee_profiles");
            b.HasKey(x => x.EmployeeId);
            b.Property(x => x.EmployeeId).ValueGeneratedNever();
            b.Ignore(x => x.DomainEvents);
            b.Ignore(x => x.Levels);
            b.HasOne<Job>().WithMany().HasForeignKey(x => x.CurrentJobId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Competencies).WithOne().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Competencies).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<EmployeeCompetency>(b =>
        {
            b.ToTable("employee_competencies");
            b.HasKey(x => new { x.EmployeeId, x.CompetencyCode });
            b.Property(x => x.CompetencyCode).HasMaxLength(50);
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
