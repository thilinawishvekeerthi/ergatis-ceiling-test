using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Talent.Shared.Auditing;
using Talent.Shared.Security;

namespace Talent.Shared.Tests;

public sealed class AuditLogTests(DatabaseFixture db) : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AddAudit_WhenSaved_StoresActorActionAndValues()
    {
        await using (var context = db.NewWidgetContext())
        {
            context.AddAudit(new SystemCurrentUser(), _clock, "Publish", "ConfigVersion", "42", confidential: true,
                beforeJson: """{"status":"Draft"}""", afterJson: """{"status":"Published"}""");
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var shared = db.NewSharedContext();
        var entry = await shared.AuditEntries.SingleAsync(TestContext.Current.CancellationToken);
        entry.ActorId.ShouldBe("system");
        entry.Action.ShouldBe("Publish");
        entry.Confidential.ShouldBeTrue();
        entry.OccurredAt.ShouldBe(_clock.GetUtcNow());
        entry.AfterJson.ShouldNotBeNull().ShouldContain("Published");
    }

    [Fact]
    public async Task Update_WhenRowExists_IsRejectedByDatabase()
    {
        await InsertEntryAsync();
        await using var shared = db.NewSharedContext();

        var ex = await Should.ThrowAsync<PostgresException>(() => shared.Database.ExecuteSqlRawAsync(
            "UPDATE shared.audit_log SET action = 'Changed'", TestContext.Current.CancellationToken));

        ex.MessageText.ShouldContain("append-only");
    }

    [Fact]
    public async Task Delete_WhenRowExists_IsRejectedByDatabase()
    {
        await InsertEntryAsync();
        await using var shared = db.NewSharedContext();

        var ex = await Should.ThrowAsync<PostgresException>(() => shared.Database.ExecuteSqlRawAsync(
            "DELETE FROM shared.audit_log", TestContext.Current.CancellationToken));

        ex.MessageText.ShouldContain("append-only");
    }

    private async Task InsertEntryAsync()
    {
        await using var context = db.NewWidgetContext();
        context.AddAudit(new SystemCurrentUser(), _clock, "Create", "Widget", "1");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
