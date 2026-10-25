using Talent.Config.Contracts;
using Talent.Config.Versions;

namespace Talent.Config.IntegrationTests;

public sealed class ConfigVersionReaderTests(ConfigDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetAsync_WhenVersionExists_ReturnsDto()
    {
        await using var context = db.NewContext();
        var (_, versionId) = await ConfigAdminServiceTests.PublishedScaleAsync(db.NewAdmin(context));

        var dto = await new ConfigVersionReader(context, db.Clock).GetAsync(versionId, Ct);

        dto.ShouldNotBeNull();
        dto.Type.ShouldBe(ConfigType.Scale);
        dto.Code.ShouldBe("PERF_5");
        dto.Status.ShouldBe(ConfigVersionStatus.Published);
    }

    [Fact]
    public async Task GetAsync_WhenUnknown_ReturnsNull()
    {
        await using var context = db.NewContext();

        (await new ConfigVersionReader(context, db.Clock).GetAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetLatestPublishedAsync_WhenNewerDraftExists_ReturnsNewestPublished()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var (definitionId, firstId) = await ConfigAdminServiceTests.PublishedScaleAsync(admin);
        await admin.CreateDraftAsync(definitionId, null, Ct);

        var dto = await new ConfigVersionReader(context, db.Clock).GetLatestPublishedAsync(ConfigType.Scale, "PERF_5", Ct);

        dto.ShouldNotBeNull().Id.ShouldBe(firstId);
    }

    [Fact]
    public async Task GetLatestPublishedAsync_WhenNothingPublished_ReturnsNull()
    {
        await using var context = db.NewContext();

        (await new ConfigVersionReader(context, db.Clock).GetLatestPublishedAsync(ConfigType.Scale, "NONE", Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task LockAsync_WhenPublished_LocksAndUpdateIsRejected()
    {
        await using var context = db.NewContext();
        var (_, versionId) = await ConfigAdminServiceTests.PublishedScaleAsync(db.NewAdmin(context));
        var reader = new ConfigVersionReader(context, db.Clock);

        await reader.LockAsync(versionId, Ct);
        await reader.LockAsync(versionId, Ct);

        (await reader.GetAsync(versionId, Ct))!.Status.ShouldBe(ConfigVersionStatus.Locked);
    }

    [Fact]
    public async Task LockAsync_WhenDraft_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);

        await Should.ThrowAsync<InvalidConfigTransitionException>(() => new ConfigVersionReader(context, db.Clock).LockAsync(versionId, Ct));
    }

    [Fact]
    public async Task LockAsync_WhenUnknown_ThrowsNotFound()
    {
        await using var context = db.NewContext();

        await Should.ThrowAsync<ConfigNotFoundException>(() => new ConfigVersionReader(context, db.Clock).LockAsync(Guid.NewGuid(), Ct));
    }
}
