using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Talent.Shared.Events;

namespace Talent.Shared.Tests;

public sealed class OutboxTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SaveChanges_WhenEntityRaisesEvent_WritesOutboxRowAndClearsEvents()
    {
        await using var context = db.NewWidgetContext();
        var widget = new Widget("a", _clock.GetUtcNow());
        var raised = widget.DomainEvents.ShouldHaveSingleItem();
        context.Add(widget);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var shared = db.NewSharedContext();
        var rows = await shared.OutboxMessages.ToListAsync(TestContext.Current.CancellationToken);
        rows.ShouldHaveSingleItem().Id.ShouldBe(raised.EventId);
        rows[0].ProcessedAt.ShouldBeNull();
        widget.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveChanges_WhenSaveFails_WritesNoOutboxRow()
    {
        await using (var first = db.NewWidgetContext())
        {
            first.Add(new Widget("dup", _clock.GetUtcNow()));
            await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var second = db.NewWidgetContext())
        {
            second.Add(new Widget("dup", _clock.GetUtcNow()));
            await Should.ThrowAsync<DbUpdateException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var shared = db.NewSharedContext();
        (await shared.OutboxMessages.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Dispatch_WhenHandlerRegistered_CallsHandlerAndMarksProcessed()
    {
        var handler = new RecordingHandler();
        await SaveWidgetAsync();

        var done = await DispatcherWith(handler).DispatchPendingAsync(10, TestContext.Current.CancellationToken);

        done.ShouldBe(1);
        handler.Received.Count.ShouldBe(1);
        await using var shared = db.NewSharedContext();
        (await shared.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken)).ProcessedAt.ShouldBe(_clock.GetUtcNow());
    }

    [Fact]
    public async Task Dispatch_WhenMessageDeliveredAgain_HandlerRunsOnlyOnce()
    {
        var handler = new RecordingHandler();
        await SaveWidgetAsync();
        await DispatcherWith(handler).DispatchPendingAsync(10, TestContext.Current.CancellationToken);

        await using (var shared = db.NewSharedContext())
        {
            await shared.OutboxMessages.ExecuteUpdateAsync(
                s => s.SetProperty(m => m.ProcessedAt, (DateTimeOffset?)null), TestContext.Current.CancellationToken);
        }

        await DispatcherWith(handler).DispatchPendingAsync(10, TestContext.Current.CancellationToken);

        handler.Received.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Dispatch_WhenHandlerThrows_KeepsMessagePendingAndRecordsError()
    {
        await SaveWidgetAsync();

        var done = await DispatcherWith(new ThrowingHandler()).DispatchPendingAsync(10, TestContext.Current.CancellationToken);

        done.ShouldBe(0);
        await using var shared = db.NewSharedContext();
        var message = await shared.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);
        message.ProcessedAt.ShouldBeNull();
        message.Attempts.ShouldBe(1);
        message.Error.ShouldNotBeNull().ShouldContain("InvalidOperationException");
    }

    [Fact]
    public async Task Dispatch_WhenNoHandler_MarksProcessed()
    {
        await SaveWidgetAsync();

        var done = await DispatcherWith().DispatchPendingAsync(10, TestContext.Current.CancellationToken);

        done.ShouldBe(1);
    }

    private async Task SaveWidgetAsync()
    {
        await using var context = db.NewWidgetContext();
        context.Add(new Widget(Guid.NewGuid().ToString("N"), _clock.GetUtcNow()));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private OutboxDispatcher DispatcherWith(params IDomainEventHandler<WidgetCreated>[] handlers)
    {
        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }

        return new OutboxDispatcher(db.NewSharedContext(), services.BuildServiceProvider(), _clock, NullLogger<OutboxDispatcher>.Instance);
    }

    private sealed class RecordingHandler : IDomainEventHandler<WidgetCreated>
    {
        public List<WidgetCreated> Received { get; } = [];

        public Task HandleAsync(WidgetCreated domainEvent, CancellationToken cancellationToken)
        {
            Received.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IDomainEventHandler<WidgetCreated>
    {
        public Task HandleAsync(WidgetCreated domainEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
