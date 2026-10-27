using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Config.Versions;
using Talent.Shared.Security;

namespace Talent.Config.IntegrationTests;

public sealed class ConfigVersionReaderTests(ConfigDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ConfigVersionReader NewReader(ConfigDbContext context, ICurrentUser? user = null) =>
        new(context, db.Clock, user ?? new SystemCurrentUser());

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetAsync_WhenVersionExists_ReturnsDto()
    {
        await using var context = db.NewContext();
        var (_, versionId) = await ConfigAdminServiceTests.PublishedScaleAsync(db.NewAdmin(context));

        var dto = await NewReader(context).GetAsync(versionId, Ct);

        dto.ShouldNotBeNull();
        dto.Type.ShouldBe(ConfigType.Scale);
        dto.Code.ShouldBe("PERF_5");
        dto.Status.ShouldBe(ConfigVersionStatus.Published);
    }

    [Fact]
    public async Task GetAsync_WhenUnknown_ReturnsNull()
    {
        await using var context = db.NewContext();

        (await NewReader(context).GetAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetLatestPublishedAsync_WhenNewerDraftExists_ReturnsNewestPublished()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var (definitionId, firstId) = await ConfigAdminServiceTests.PublishedScaleAsync(admin);
        await admin.CreateDraftAsync(definitionId, null, Ct);

        var dto = await NewReader(context).GetLatestPublishedAsync(ConfigType.Scale, "PERF_5", Ct);

        dto.ShouldNotBeNull().Id.ShouldBe(firstId);
    }

    [Fact]
    public async Task GetLatestPublishedAsync_WhenNothingPublished_ReturnsNull()
    {
        await using var context = db.NewContext();

        (await NewReader(context).GetLatestPublishedAsync(ConfigType.Scale, "NONE", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task LockAsync_WhenPublished_LocksAndUpdateIsRejected()
    {
        await using var context = db.NewContext();
        var (_, versionId) = await ConfigAdminServiceTests.PublishedScaleAsync(db.NewAdmin(context));
        var reader = NewReader(context);

        await reader.LockAsync(versionId, Ct);
        await reader.LockAsync(versionId, Ct);

        (await reader.GetAsync(versionId, Ct))!.Status.ShouldBe(ConfigVersionStatus.Locked);
    }

    [Fact]
    public async Task LockAsync_WhenSignedInUserLocks_AuditNamesThatUser()
    {
        await using var context = db.NewContext();
        var (_, versionId) = await ConfigAdminServiceTests.PublishedScaleAsync(db.NewAdmin(context));

        await NewReader(context, new TestUser("user-42")).LockAsync(versionId, Ct);

        await using var shared = db.NewSharedContext();
        var entry = await shared.AuditEntries.SingleAsync(a => a.Action == "Lock" && a.ObjectId == versionId.ToString(), Ct);
        entry.ActorId.ShouldBe("user-42");
    }

    [Fact]
    public async Task LockAsync_WhenAnotherCallerLocksFirst_StillSucceeds()
    {
        await using var admin = db.NewContext();
        var (_, versionId) = await ConfigAdminServiceTests.PublishedScaleAsync(db.NewAdmin(admin));
        await using var first = db.NewContext();
        await using var second = db.NewContext();
        var firstReader = NewReader(first);
        var secondReader = NewReader(second);

        // Both callers see Published; the second save hits the changed xmin.
        await first.Versions.SingleAsync(v => v.Id == versionId, Ct);
        await second.Versions.SingleAsync(v => v.Id == versionId, Ct);
        await firstReader.LockAsync(versionId, Ct);
        await secondReader.LockAsync(versionId, Ct);

        await using var read = db.NewContext();
        (await NewReader(read).GetAsync(versionId, Ct))!.Status.ShouldBe(ConfigVersionStatus.Locked);
    }

    private sealed class TestUser(string id) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public string UserId => id;
    }

    [Fact]
    public async Task LockAsync_WhenDraft_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);

        await Should.ThrowAsync<InvalidConfigTransitionException>(() => NewReader(context).LockAsync(versionId, Ct));
    }

    [Fact]
    public async Task LockAsync_WhenUnknown_ThrowsNotFound()
    {
        await using var context = db.NewContext();

        await Should.ThrowAsync<ConfigNotFoundException>(() => NewReader(context).LockAsync(Guid.NewGuid(), Ct));
    }
}
