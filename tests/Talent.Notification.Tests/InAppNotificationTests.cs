using Talent.Notification.Inbox;

namespace Talent.Notification.Tests;

public sealed class InAppNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static InAppNotification New(string? link = "/manager") =>
        InAppNotification.Create(Guid.NewGuid(), "assessment.cycle-opened", Guid.NewGuid(), " Cycle open ", " Please rate. ", link, Now);

    [Fact]
    public void Create_StartsUnread_AndTrimsText()
    {
        var alert = New();

        alert.IsRead.ShouldBeFalse();
        (alert.Title, alert.Body, alert.CreatedAt).ShouldBe(("Cycle open", "Please rate.", Now));
    }

    [Fact]
    public void MarkRead_KeepsTheFirstTime()
    {
        var alert = New();

        alert.MarkRead(Now.AddHours(1));
        alert.MarkRead(Now.AddHours(5));

        alert.IsRead.ShouldBeTrue();
        alert.ReadAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Create_WhenRecipientEmpty_Throws() =>
        Should.Throw<InvalidNotificationException>(() => InAppNotification.Create(Guid.Empty, "k", Guid.NewGuid(), "t", "b", null, Now));

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("manager")]
    [InlineData("/ok\nhttps://evil.example")]
    public void Create_WhenLinkLeavesTheApp_Throws(string link) =>
        Should.Throw<InvalidNotificationException>(() => New(link));

    [Theory]
    [InlineData("/")]
    [InlineData("/manager")]
    [InlineData("/admin/cycles/123/board")]
    public void Create_AllowsLocalPaths(string link) => New(link).LinkUrl.ShouldBe(link);

    [Fact]
    public void Create_AllowsNoLink() => New(null).LinkUrl.ShouldBeNull();
}
