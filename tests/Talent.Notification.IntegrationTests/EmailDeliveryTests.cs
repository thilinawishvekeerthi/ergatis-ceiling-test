using Microsoft.Extensions.Time.Testing;
using Talent.Notification.Email;
using static Talent.Notification.IntegrationTests.NotificationHostFixture;

namespace Talent.Notification.IntegrationTests;

/// <summary>The mail queue against a real database with a controllable clock: success, retry with spacing, give up, and one bad mail never blocks the rest.</summary>
public sealed class EmailDeliveryTests(NotificationHostFixture host) : IClassFixture<NotificationHostFixture>
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 3, 1, 8, 0, 0, TimeSpan.Zero));

    private async Task<EmailMessage> QueueAsync(string? to = "someone@example.com", string subject = "Hello")
    {
        await using var db = host.NewContext();
        var mail = EmailMessage.Create(Guid.NewGuid(), to, "career.profile-updated", Guid.NewGuid(), $"{subject} {Guid.NewGuid():N}", "Body", _clock.GetUtcNow());
        db.Emails.Add(mail);
        await db.SaveChangesAsync(Ct);
        return mail;
    }

    private async Task<EmailMessage> ReloadAsync(Guid id)
    {
        await using var db = host.NewContext();
        return db.Emails.Single(e => e.Id == id);
    }

    private async Task<int> RunAsync(IEmailSender sender, int batch = 50)
    {
        await using var db = host.NewContext();
        return await new EmailDeliveryService(db, sender, _clock).SendPendingAsync(batch, Ct);
    }

    private sealed class FailingSender(string message = "smtp down") : IEmailSender
    {
        public int Calls { get; private set; }

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException(message);
        }
    }

    [Fact]
    public async Task Send_MarksTheEmailSentWithTheTime()
    {
        var mail = await QueueAsync();
        var sender = new RecordingEmailSender();

        (await RunAsync(sender)).ShouldBeGreaterThanOrEqualTo(1);

        var sent = await ReloadAsync(mail.Id);
        (sent.Status, sent.Attempts, sent.SentAt).ShouldBe((EmailStatus.Sent, 1, (DateTimeOffset?)_clock.GetUtcNow()));
        sender.Sent.Any(m => m.Subject == mail.Subject && m.To == "someone@example.com").ShouldBeTrue();
    }

    [Fact]
    public async Task Failure_IsRecordedAndRetriedLaterNotImmediately()
    {
        var mail = await QueueAsync();
        var failing = new FailingSender();

        await RunAsync(failing);
        var first = await ReloadAsync(mail.Id);
        (first.Status, first.Attempts).ShouldBe((EmailStatus.Pending, 1));
        first.Error.ShouldNotBeNull().ShouldContain("smtp down");

        var callsAfterFirst = failing.Calls;
        await RunAsync(failing); // same minute: not due yet
        failing.Calls.ShouldBe(callsAfterFirst);

        _clock.Advance(TimeSpan.FromMinutes(1));
        await RunAsync(new RecordingEmailSender());
        var recovered = await ReloadAsync(mail.Id);
        (recovered.Status, recovered.Attempts, recovered.Error).ShouldBe((EmailStatus.Sent, 2, (string?)null));
    }

    [Fact]
    public async Task AfterFiveFailures_TheEmailIsFailedForGood()
    {
        var mail = await QueueAsync();
        var failing = new FailingSender();

        for (var i = 0; i < EmailMessage.MaxAttempts; i++)
        {
            await RunAsync(failing);
            _clock.Advance(TimeSpan.FromMinutes(10));
        }

        var failed = await ReloadAsync(mail.Id);
        (failed.Status, failed.Attempts).ShouldBe((EmailStatus.Failed, EmailMessage.MaxAttempts));
        var callsWhenFailed = failing.Calls;
        await RunAsync(failing);
        failing.Calls.ShouldBe(callsWhenFailed);
    }

    [Fact]
    public async Task OneFailingEmail_DoesNotStopTheOthers()
    {
        var bad = await QueueAsync("bad@example.com", "Bad");
        var good = await QueueAsync("good@example.com", "Good");
        var sender = new SelectiveSender("bad@example.com");

        await RunAsync(sender);

        (await ReloadAsync(bad.Id)).Status.ShouldBe(EmailStatus.Pending);
        (await ReloadAsync(good.Id)).Status.ShouldBe(EmailStatus.Sent);
    }

    [Fact]
    public async Task SkippedEmails_AreNeverSent()
    {
        var skipped = await QueueAsync(to: null);
        var sender = new RecordingEmailSender();

        await RunAsync(sender);

        (await ReloadAsync(skipped.Id)).Status.ShouldBe(EmailStatus.Skipped);
        sender.Sent.Any(m => m.Subject == skipped.Subject).ShouldBeFalse();
    }

    [Fact]
    public async Task BatchSize_LimitsHowManyAreSentPerPass()
    {
        for (var i = 0; i < 3; i++)
        {
            await QueueAsync(subject: "Batch");
        }

        var sender = new RecordingEmailSender();
        var sentNow = await RunAsync(sender, batch: 2);

        sentNow.ShouldBe(2);
    }

    private sealed class SelectiveSender(string failFor) : IEmailSender
    {
        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken) =>
            to == failFor ? throw new InvalidOperationException("rejected") : Task.CompletedTask;
    }
}
