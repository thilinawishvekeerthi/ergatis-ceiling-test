using Microsoft.EntityFrameworkCore;
using Talent.Shared.Events;
using Talent.Shared.Persistence;

namespace Talent.Shared.Tests;

public sealed record WidgetCreated(DateTimeOffset OccurredAt, Guid WidgetId) : DomainEvent(OccurredAt);

/// <summary>A stand-in for a module aggregate that raises events.</summary>
public sealed class Widget : IHasDomainEvents
{
    private readonly List<IDomainEvent> _events = [];

    private Widget() => Name = "";

    public Widget(string name, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        Name = name;
        _events.Add(new WidgetCreated(now, Id));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    public void ClearDomainEvents() => _events.Clear();
}

/// <summary>A stand-in module context. It maps the shared tables the same way a real module does.</summary>
public sealed class WidgetDbContext(DbContextOptions<WidgetDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(b =>
        {
            b.ToTable("widgets", "public");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Ignore(x => x.DomainEvents);
        });
        modelBuilder.MapSharedTables(ownedByThisContext: false);
        modelBuilder.UseSnakeCaseNames();
    }
}
