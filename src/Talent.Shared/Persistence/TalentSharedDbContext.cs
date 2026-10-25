using Microsoft.EntityFrameworkCore;
using Talent.Shared.Auditing;

namespace Talent.Shared.Persistence;

/// <summary>Owns the shared schema: outbox, processed events and the audit log.</summary>
public sealed class TalentSharedDbContext(DbContextOptions<TalentSharedDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(SharedModel.Schema);
        modelBuilder.MapSharedTables(ownedByThisContext: true);
        modelBuilder.UseSnakeCaseNames();
    }
}
