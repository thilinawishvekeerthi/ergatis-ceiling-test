using Talent.Config.Versions;
using Talent.Config.Contracts;

namespace Talent.Config.Tests.Versions;

public sealed class ConfigVersionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 5, 8, 0, 0, TimeSpan.Zero);

    private static ConfigVersion NewDraft(string payload = """{"levels":5}""") =>
        ConfigVersion.CreateDraft(Guid.NewGuid(), 1, payload);

    [Fact]
    public void CreateDraft_WhenCalled_StartsAsDraftWithoutChecksum()
    {
        var version = NewDraft();

        version.Status.ShouldBe(ConfigVersionStatus.Draft);
        version.Checksum.ShouldBeNull();
    }

    [Fact]
    public void CreateDraft_WhenPayloadIsBlank_Throws() =>
        Should.Throw<ArgumentException>(() => NewDraft("  "));

    [Fact]
    public void UpdatePayload_WhenDraft_ReplacesPayload()
    {
        var version = NewDraft();

        version.UpdatePayload("""{"levels":3}""");

        version.PayloadJson.ShouldBe("""{"levels":3}""");
    }

    [Fact]
    public void Publish_WhenDraft_SetsChecksumAndPublishedFields()
    {
        var version = NewDraft();

        version.Publish("hr.admin", Now);

        version.Status.ShouldBe(ConfigVersionStatus.Published);
        version.Checksum.ShouldNotBeNullOrEmpty();
        version.PublishedBy.ShouldBe("hr.admin");
        version.PublishedAt.ShouldBe(Now);
    }

    [Fact]
    public void Publish_WhenSamePayload_GivesSameChecksum()
    {
        var a = NewDraft();
        var b = NewDraft();

        a.Publish("x", Now);
        b.Publish("y", Now);

        a.Checksum.ShouldBe(b.Checksum);
    }

    [Fact]
    public void Publish_WhenPayloadDiffersOnlyInKeyOrderAndWhitespace_GivesSameChecksum()
    {
        var a = ConfigVersion.CreateDraft(Guid.NewGuid(), 1, """{"a":1,"b":{"x":[1,2],"y":"z"}}""");
        var b = ConfigVersion.CreateDraft(Guid.NewGuid(), 1, """{ "b": { "y": "z", "x": [1, 2] }, "a": 1 }""");

        a.Publish("x", Now);
        b.Publish("x", Now);

        a.Checksum.ShouldBe(b.Checksum);
    }

    [Fact]
    public void Publish_WhenPayloadValuesDiffer_GivesDifferentChecksum()
    {
        var a = ConfigVersion.CreateDraft(Guid.NewGuid(), 1, """{"a":1}""");
        var b = ConfigVersion.CreateDraft(Guid.NewGuid(), 1, """{"a":2}""");

        a.Publish("x", Now);
        b.Publish("x", Now);

        a.Checksum.ShouldNotBe(b.Checksum);
    }

    [Fact]
    public void Publish_WhenPayloadIsNotJson_Throws()
    {
        var version = ConfigVersion.CreateDraft(Guid.NewGuid(), 1, "not json");

        Should.Throw<InvalidConfigPayloadException>(() => version.Publish("x", Now));
    }

    [Fact]
    public void Publish_WhenAlreadyPublished_Throws()
    {
        var version = NewDraft();
        version.Publish("x", Now);

        Should.Throw<InvalidConfigTransitionException>(() => version.Publish("x", Now));
    }

    [Fact]
    public void UpdatePayload_WhenPublished_Throws()
    {
        var version = NewDraft();
        version.Publish("x", Now);

        Should.Throw<ConfigVersionLockedException>(() => version.UpdatePayload("{}"));
    }

    [Fact]
    public void UpdatePayload_WhenLocked_Throws()
    {
        var version = NewDraft();
        version.Publish("x", Now);
        version.Lock();

        Should.Throw<ConfigVersionLockedException>(() => version.UpdatePayload("{}"));
    }

    [Fact]
    public void Lock_WhenDraft_Throws() =>
        Should.Throw<InvalidConfigTransitionException>(() => NewDraft().Lock());

    [Fact]
    public void Lock_WhenAlreadyLocked_DoesNothing()
    {
        var version = NewDraft();
        version.Publish("x", Now);
        version.Lock();

        version.Lock();

        version.Status.ShouldBe(ConfigVersionStatus.Locked);
    }

    [Fact]
    public void Retire_WhenLocked_MovesToRetired()
    {
        var version = NewDraft();
        version.Publish("x", Now);
        version.Lock();

        version.Retire();

        version.Status.ShouldBe(ConfigVersionStatus.Retired);
    }

    [Fact]
    public void Retire_WhenDraft_Throws() =>
        Should.Throw<InvalidConfigTransitionException>(() => NewDraft().Retire());

    [Fact]
    public void CreateNextVersion_WhenLocked_CopiesPayloadAsNewDraft()
    {
        var version = NewDraft("""{"levels":5}""");
        version.Publish("x", Now);
        version.Lock();

        var next = version.CreateNextVersion();

        next.Status.ShouldBe(ConfigVersionStatus.Draft);
        next.VersionNo.ShouldBe(2);
        next.DefinitionId.ShouldBe(version.DefinitionId);
        next.PayloadJson.ShouldBe(version.PayloadJson);
        next.Id.ShouldNotBe(version.Id);
    }

    [Fact]
    public void CreateNextVersion_WhenDraft_Throws() =>
        Should.Throw<InvalidConfigTransitionException>(() => NewDraft().CreateNextVersion());

    [Fact]
    public void CreateNextVersion_LeavesOriginalUnchanged()
    {
        var version = NewDraft();
        version.Publish("x", Now);
        version.Lock();

        var next = version.CreateNextVersion();
        next.UpdatePayload("""{"levels":4}""");

        version.PayloadJson.ShouldBe("""{"levels":5}""");
    }
}
