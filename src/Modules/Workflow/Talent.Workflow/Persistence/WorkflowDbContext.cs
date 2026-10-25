using Microsoft.EntityFrameworkCore;
using Talent.Shared.Persistence;
using Talent.Workflow.Instances;
using Talent.Workflow.Versions;

namespace Talent.Workflow.Persistence;

internal sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public const string Schema = "workflow";

    public DbSet<WorkflowDefinition> Definitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowVersion> Versions => Set<WorkflowVersion>();
    public DbSet<WorkflowInstance> Instances => Set<WorkflowInstance>();
    public DbSet<HistoryEntry> History => Set<HistoryEntry>();
    public DbSet<ApprovalTask> Tasks => Set<ApprovalTask>();
    public DbSet<TaskAssignee> Assignees => Set<TaskAssignee>();
    public DbSet<Delegation> Delegations => Set<Delegation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<WorkflowDefinition>(b =>
        {
            b.ToTable("workflow_definitions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Code).HasMaxLength(50);
            b.Property(x => x.Name).HasMaxLength(200);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<WorkflowVersion>(b =>
        {
            b.ToTable("workflow_versions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.PayloadJson).HasColumnType("jsonb");
            b.Property(x => x.Checksum).HasMaxLength(64);
            b.Property(x => x.PublishedBy).HasMaxLength(200);
            b.Ignore(x => x.DomainEvents);
            b.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.DefinitionId, x.VersionNo }).IsUnique();
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<WorkflowInstance>(b =>
        {
            b.ToTable("workflow_instances");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.DefinitionCode).HasMaxLength(50);
            b.Property(x => x.SubjectType).HasMaxLength(50);
            b.Property(x => x.SubjectId).HasMaxLength(100);
            b.Property(x => x.SubjectEmployeeId).HasMaxLength(100);
            b.Property(x => x.CurrentState).HasMaxLength(50);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.ContextJson).HasColumnType("jsonb");
            b.Property(x => x.StartedBy).HasMaxLength(200);
            b.Ignore(x => x.Context);
            b.Ignore(x => x.OpenTask);
            b.Ignore(x => x.DomainEvents);
            b.HasOne<WorkflowVersion>().WithMany().HasForeignKey(x => x.DefinitionVersionId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.History).WithOne().HasForeignKey(h => h.InstanceId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Tasks).WithOne().HasForeignKey(t => t.InstanceId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Navigation(x => x.Tasks).HasField("_tasks").UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(x => new { x.SubjectType, x.SubjectId });
            // One running workflow per subject.
            b.HasIndex(x => new { x.SubjectType, x.SubjectId }, "ux_workflow_instances_active_subject")
                .IsUnique().HasFilter("status = 'Active'");
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<HistoryEntry>(b =>
        {
            b.ToTable("workflow_history");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.FromState).HasMaxLength(50);
            b.Property(x => x.ToState).HasMaxLength(50);
            b.Property(x => x.Action).HasMaxLength(50);
            b.Property(x => x.Actor).HasMaxLength(200);
            b.Property(x => x.Comment).HasMaxLength(2000);
            b.HasIndex(x => new { x.InstanceId, x.Sequence }).IsUnique();
        });

        modelBuilder.Entity<ApprovalTask>(b =>
        {
            b.ToTable("approval_tasks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.State).HasMaxLength(50);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.CompletedBy).HasMaxLength(200);
            b.HasMany(x => x.Assignees).WithOne().HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Assignees).HasField("_assignees").UsePropertyAccessMode(PropertyAccessMode.Field);
            // Serves the SLA tick.
            b.HasIndex(x => new { x.Status, x.DueAt });
        });

        modelBuilder.Entity<TaskAssignee>(b =>
        {
            b.ToTable("approval_task_assignees");
            b.HasKey(x => new { x.TaskId, x.UserId });
            b.Property(x => x.TaskId).ValueGeneratedNever();
            b.Property(x => x.UserId).HasMaxLength(200);
            b.Property(x => x.DelegatedFrom).HasMaxLength(200);
            // Serves the inbox.
            b.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<Delegation>(b =>
        {
            b.ToTable("delegations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.DelegatorId).HasMaxLength(200);
            b.Property(x => x.DelegateId).HasMaxLength(200);
            b.HasIndex(x => x.DelegatorId);
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
