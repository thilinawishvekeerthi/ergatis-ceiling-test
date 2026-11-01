using Talent.Notification.Email;

namespace Talent.Notification.Tests;

public sealed class EmailMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static EmailMessage New(string? to = "ada@example.com") =>
        EmailMessage.Create(Guid.NewGuid(), to, "career.profile-updated", Guid.NewGuid(), " Subject ", " Body ", Now);

    [Fact]
    public void Create_WithAddress_IsPending()
    {
        var mail = New();

        (mail.Status, mail.Attempts, mail.Subject, mail.Body).ShouldBe((EmailStatus.Pending, 0, "Subject", "Body"));
        mail.Error.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutAddress_IsSkippedWithAReason(string? to)
    {
        var mail = New(to);

        mail.Status.ShouldBe(EmailStatus.Skipped);
        mail.Error.ShouldNotBeNull();
    }

    [Fact]
    public void MarkSent_RecordsTimeAndCountsTheAttempt()
    {
        var mail = New();

        mail.MarkSent(Now.AddMinutes(1));

        (mail.Status, mail.SentAt, mail.Attempts).ShouldBe((EmailStatus.Sent, (DateTimeOffset?)Now.AddMinutes(1), 1));
    }

    [Fact]
    public void MarkAttemptFailed_StaysPendingUntilTheLastAttempt()
    {
        var mail = New();

        for (var i = 1; i < EmailMessage.MaxAttempts; i++)
        {
            mail.MarkAttemptFailed("smtp down");
            mail.Status.ShouldBe(EmailStatus.Pending);
            mail.Attempts.ShouldBe(i);
        }

        mail.MarkAttemptFailed("smtp down");

        (mail.Status, mail.Attempts, mail.Error).ShouldBe((EmailStatus.Failed, EmailMessage.MaxAttempts, "smtp down"));
    }

    [Fact]
    public void MarkSent_AfterEarlierFailures_ClearsTheError()
    {
        var mail = New();
        mail.MarkAttemptFailed("timeout");

        mail.MarkSent(Now);

        (mail.Status, mail.Error, mail.Attempts).ShouldBe((EmailStatus.Sent, (string?)null, 2));
    }

    [Fact]
    public void ErrorText_IsCapped()
    {
        var mail = New();

        mail.MarkAttemptFailed(new string('x', 2000));

        mail.Error!.Length.ShouldBe(500);
    }

    [Fact]
    public void CannotSendOrFailAnEmailThatIsNotPending()
    {
        var sent = New();
        sent.MarkSent(Now);
        Should.Throw<InvalidNotificationException>(() => sent.MarkSent(Now));
        Should.Throw<InvalidNotificationException>(() => sent.MarkAttemptFailed("x"));
        Should.Throw<InvalidNotificationException>(() => New(null).MarkSent(Now));
    }

    [Fact]
    public void Create_WhenRecipientEmpty_Throws() =>
        Should.Throw<InvalidNotificationException>(() => EmailMessage.Create(Guid.Empty, "a@b.c", "k", Guid.NewGuid(), "s", "b", Now));
}
