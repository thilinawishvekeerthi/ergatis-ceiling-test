using Microsoft.EntityFrameworkCore;
using Talent.Shared.Auditing;

namespace Talent.Shared.Persistence;

public static class SharedModel
{
    public const string Schema = "shared";

    /// <summary>
    /// Maps the shared tables. The shared context owns them (<paramref name="ownedByThisContext"/> true).
    /// Module contexts map them too so one SaveChanges writes event and audit rows in the same transaction,
    /// but never create them in their own migrations.
    /// </summary>
    public static void MapSharedTables(this ModelBuilder modelBuilder, bool ownedByThisContext)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages", Schema, t => Exclude(t, ownedByThisContext));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EventType).HasMaxLength(500).IsRequired();
            b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.Error).HasMaxLength(2000);
            b.HasIndex(x => x.ProcessedAt).HasFilter("processed_at IS NULL").HasDatabaseName("ix_outbox_pending");
        });

        modelBuilder.Entity<ProcessedEvent>(b =>
        {
            b.ToTable("processed_events", Schema, t => Exclude(t, ownedByThisContext));
            b.HasKey(x => new { x.Handler, x.EventId });
            b.Property(x => x.Handler).HasMaxLength(300);
        });

        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("audit_log", Schema, t => Exclude(t, ownedByThisContext));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityAlwaysColumn();
            b.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
            b.Property(x => x.Action).HasMaxLength(100).IsRequired();
            b.Property(x => x.ObjectType).HasMaxLength(100).IsRequired();
            b.Property(x => x.ObjectId).HasMaxLength(100).IsRequired();
            b.Property(x => x.BeforeJson).HasColumnType("jsonb");
            b.Property(x => x.AfterJson).HasColumnType("jsonb");
            b.HasIndex(x => new { x.ObjectType, x.ObjectId });
        });
    }

    private static void Exclude(Microsoft.EntityFrameworkCore.Metadata.Builders.TableBuilder table, bool owned)
    {
        if (!owned)
        {
            table.ExcludeFromMigrations();
        }
    }
}
