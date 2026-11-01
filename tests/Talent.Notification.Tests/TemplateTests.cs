using Talent.Notification.Templates;

namespace Talent.Notification.Tests;

public sealed class TemplateTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, string> Values(params (string K, string V)[] v) => v.ToDictionary(x => x.K, x => x.V);

    [Fact]
    public void Render_FillsPlaceholders()
    {
        TemplateRenderer.Render("Cycle {CycleName} is open. {CycleName}!", Values(("CycleName", "2026 annual")))
            .ShouldBe("Cycle 2026 annual is open. 2026 annual!");
    }

    [Fact]
    public void Render_MissingValueBecomesEmpty() =>
        TemplateRenderer.Render("Hello {Name}.", Values()).ShouldBe("Hello .");

    [Fact]
    public void Render_DoesNotReadPlaceholdersInsideValues() =>
        TemplateRenderer.Render("Cycle {CycleName}", Values(("CycleName", "{Other}"), ("Other", "boom"))).ShouldBe("Cycle {Other}");

    [Fact]
    public void Render_LeavesBracesThatAreNotPlaceholders() =>
        TemplateRenderer.Render("{ not one } {1}", Values()).ShouldBe("{ not one } {1}");

    [Fact]
    public void PlaceholdersIn_ListsEachOnce() =>
        TemplateRenderer.PlaceholdersIn("{A} {B} {A}").ShouldBe(["A", "B"]);

    [Fact]
    public void Create_AcceptsPlaceholdersOfItsEvent()
    {
        var template = MessageTemplate.Create(EventCatalog.CycleOpened, NotificationChannel.Email, "  {CycleName} is open ", "Please rate your team in {CycleName}.", true, "hr", Now);

        template.Subject.ShouldBe("{CycleName} is open");
        template.IsActive.ShouldBeTrue();
        template.UpdatedBy.ShouldBe("hr");
        template.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_WhenPlaceholderNotAllowed_ThrowsAndNamesIt()
    {
        var ex = Should.Throw<InvalidNotificationException>(() =>
            MessageTemplate.Create(EventCatalog.CycleOpened, NotificationChannel.InApp, "Hi", "Dear {FirstName}", true, "hr", Now));

        ex.Message.ShouldContain("{FirstName}");
        ex.Message.ShouldContain("{CycleName}");
    }

    [Fact]
    public void Create_ForEventWithNoPlaceholders_RejectsAny() =>
        Should.Throw<InvalidNotificationException>(() =>
            MessageTemplate.Create(EventCatalog.ProfileUpdated, NotificationChannel.InApp, "Updated {X}", "Body", true, "hr", Now)).Message.ShouldContain("none");

    [Fact]
    public void Create_WhenEventUnknown_Throws() =>
        Should.Throw<InvalidNotificationException>(() => MessageTemplate.Create("no.such", NotificationChannel.InApp, "S", "B", true, "hr", Now));

    [Theory]
    [InlineData("", "Body")]
    [InlineData("  ", "Body")]
    [InlineData("Subject", "")]
    public void Create_WhenSubjectOrBodyBlank_Throws(string subject, string body) =>
        Should.Throw<InvalidNotificationException>(() => MessageTemplate.Create(EventCatalog.ProfileUpdated, NotificationChannel.InApp, subject, body, true, "hr", Now));

    [Fact]
    public void Create_WhenTooLong_Throws()
    {
        Should.Throw<InvalidNotificationException>(() => MessageTemplate.Create(EventCatalog.ProfileUpdated, NotificationChannel.InApp, new string('x', MessageTemplate.MaxSubject + 1), "B", true, "hr", Now));
        Should.Throw<InvalidNotificationException>(() => MessageTemplate.Create(EventCatalog.ProfileUpdated, NotificationChannel.InApp, "S", new string('x', MessageTemplate.MaxBody + 1), true, "hr", Now));
    }

    [Fact]
    public void Set_ChangesTextAndSwitch()
    {
        var template = MessageTemplate.Create(EventCatalog.ProfileUpdated, NotificationChannel.Email, "Old", "Old body", true, "hr", Now);

        template.Set("New", "New body", false, "other.hr", Now.AddDays(1));

        (template.Subject, template.Body, template.IsActive, template.UpdatedBy).ShouldBe(("New", "New body", false, "other.hr"));
        template.UpdatedAt.ShouldBe(Now.AddDays(1));
    }

    [Fact]
    public void Catalog_EveryEventHasAUniqueKey_AndOnlyLocalDefaultLinks()
    {
        EventCatalog.All.Select(e => e.Key).Distinct().Count().ShouldBe(EventCatalog.All.Count);
        EventCatalog.All.Where(e => e.DefaultLink is not null).ShouldAllBe(e => Talent.Notification.Inbox.InAppNotification.IsLocalPath(e.DefaultLink!));
    }
}
