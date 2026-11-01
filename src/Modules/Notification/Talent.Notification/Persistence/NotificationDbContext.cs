using Microsoft.EntityFrameworkCore;
using Talent.Notification.Email;
using Talent.Notification.Inbox;
using Talent.Notification.Templates;
using Talent.Shared.Persistence;

namespace Talent.Notification.Persistence;

internal sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public const string Schema = "notification";

    public DbSet<MessageTemplate> Templates => Set<MessageTemplate>();
    public DbSet<InAppNotification> Notifications => Set<InAppNotification>();
    public DbSet<EmailMessage> Emails => Set<EmailMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<MessageTemplate>(b =>
        {
            b.ToTable("message_templates");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EventKey).HasMaxLength(100);
            b.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Subject).HasMaxLength(MessageTemplate.MaxSubject);
            b.Property(x => x.Body).HasMaxLength(MessageTemplate.MaxBody);
            b.Property(x => x.UpdatedBy).HasMaxLength(200);
            b.HasIndex(x => new { x.EventKey, x.Channel }).IsUnique();
            b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        modelBuilder.Entity<InAppNotification>(b =>
        {
            b.ToTable("notifications");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EventKey).HasMaxLength(100);
            b.Property(x => x.Title).HasMaxLength(MessageTemplate.MaxSubject);
            b.Property(x => x.Body).HasMaxLength(MessageTemplate.MaxBody);
            b.Property(x => x.LinkUrl).HasMaxLength(500);
            b.Ignore(x => x.IsRead);
            // One alert per source and recipient: a second delivery of the same event creates nothing (ADR-10).
            b.HasIndex(x => new { x.SourceId, x.RecipientId, x.EventKey }).IsUnique();
            b.HasIndex(x => new { x.RecipientId, x.CreatedAt });
        });

        modelBuilder.Entity<EmailMessage>(b =>
        {
            b.ToTable("emails");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ToAddress).HasMaxLength(320);
            b.Property(x => x.EventKey).HasMaxLength(100);
            b.Property(x => x.Subject).HasMaxLength(MessageTemplate.MaxSubject);
            b.Property(x => x.Body).HasMaxLength(MessageTemplate.MaxBody);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Error).HasMaxLength(500);
            b.HasIndex(x => new { x.SourceId, x.RecipientId, x.EventKey }).IsUnique();
            b.HasIndex(x => x.Status);
        });

        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
