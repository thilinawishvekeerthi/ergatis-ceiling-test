using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Config.Versions;

namespace Talent.Config.IntegrationTests;

public sealed class ConfigAdminServiceTests(ConfigDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateDefinition_WhenNew_Persists()
    {
        await using var context = db.NewContext();

        var id = await db.NewAdmin(context).CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "Performance 5 level", Ct);

        await using var read = db.NewContext();
        var definition = await read.Definitions.SingleAsync(d => d.Id == id, Ct);
        definition.Type.ShouldBe(ConfigType.Scale);
        definition.Code.ShouldBe("PERF_5");
    }

    [Fact]
    public async Task CreateDefinition_WhenTypeAndCodeExist_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "A", Ct);

        await Should.ThrowAsync<ConfigConflictException>(() => admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "B", Ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("lower")]
    [InlineData("HAS SPACE")]
    public async Task CreateDefinition_WhenCodeInvalid_Throws(string code)
    {
        await using var context = db.NewContext();

        await Should.ThrowAsync<InvalidConfigPayloadException>(
            () => db.NewAdmin(context).CreateDefinitionAsync(ConfigType.Scale, code, "x", Ct));
    }

    [Fact]
    public async Task CreateDraft_WhenFirstVersion_UsesGivenPayloadAsVersion1()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);

        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);

        var version = await context.Versions.SingleAsync(v => v.Id == versionId, Ct);
        version.VersionNo.ShouldBe(1);
        version.Status.ShouldBe(ConfigVersionStatus.Draft);
    }

    [Fact]
    public async Task CreateDraft_WhenFirstVersionHasNoPayload_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);

        await Should.ThrowAsync<InvalidConfigPayloadException>(() => admin.CreateDraftAsync(definitionId, null, Ct));
    }

    [Fact]
    public async Task CreateDraft_WhenDraftAlreadyOpen_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);

        await Should.ThrowAsync<ConfigConflictException>(() => admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct));
    }

    [Fact]
    public async Task CreateDraft_WhenPreviousPublishedAndNoPayload_CopiesPreviousAsNextVersion()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var (definitionId, firstId) = await PublishedScaleAsync(admin);

        var nextId = await admin.CreateDraftAsync(definitionId, null, Ct);

        var next = await context.Versions.SingleAsync(v => v.Id == nextId, Ct);
        next.VersionNo.ShouldBe(2);
        next.Status.ShouldBe(ConfigVersionStatus.Draft);
        var original = (await context.Versions.AsNoTracking().SingleAsync(v => v.Id == firstId, Ct)).PayloadJson;
        // jsonb may reformat the text, so compare the JSON content.
        CanonicalJson.Canonicalize(next.PayloadJson).ShouldBe(CanonicalJson.Canonicalize(original));
    }

    [Fact]
    public async Task UpdateDraft_WhenDraft_ReplacesPayload()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);

        await admin.UpdateDraftAsync(versionId, Payloads.Scale1Level, Ct);

        await using var read = db.NewContext();
        var stored = (await read.Versions.SingleAsync(v => v.Id == versionId, Ct)).PayloadJson;
        // jsonb may reorder keys and change spacing, so compare the JSON, not the text.
        System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(stored), System.Text.Json.Nodes.JsonNode.Parse(Payloads.Scale1Level)).ShouldBeTrue();
    }

    [Fact]
    public async Task Publish_WhenScaleValid_PublishesProjectsLevelsAndRecordsAuditAndEvent()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context, "hr.admin");
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);

        await admin.PublishAsync(versionId, Ct);

        await using var read = db.NewContext();
        var version = await read.Versions.SingleAsync(v => v.Id == versionId, Ct);
        version.Status.ShouldBe(ConfigVersionStatus.Published);
        version.Checksum.ShouldNotBeNullOrEmpty();
        version.PublishedBy.ShouldBe("hr.admin");
        version.PublishedAt.ShouldBe(db.Clock.GetUtcNow());

        var levels = await read.ScaleLevels.Where(l => l.VersionId == versionId).OrderBy(l => l.Level).ToListAsync(Ct);
        levels.Count.ShouldBe(5);
        levels[4].Band.ShouldBe("HIGH");

        await using var shared = db.NewSharedContext();
        (await shared.AuditEntries.AnyAsync(a => a.Action == "Publish" && a.ObjectId == versionId.ToString(), Ct)).ShouldBeTrue();
        (await shared.OutboxMessages.AnyAsync(m => m.EventType.Contains("ConfigVersionPublished"), Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Publish_WhenScaleHasOneLevel_ThrowsAndStaysDraft()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale1Level, Ct);

        await Should.ThrowAsync<InvalidConfigPayloadException>(() => admin.PublishAsync(versionId, Ct));

        await using var read = db.NewContext();
        (await read.Versions.SingleAsync(v => v.Id == versionId, Ct)).Status.ShouldBe(ConfigVersionStatus.Draft);
        (await read.ScaleLevels.AnyAsync(Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Publish_WhenGridValid_Publishes()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Grid, "NINE_BOX", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Grid2x2, Ct);

        await admin.PublishAsync(versionId, Ct);

        (await context.Versions.SingleAsync(v => v.Id == versionId, Ct)).Status.ShouldBe(ConfigVersionStatus.Published);
    }

    [Fact]
    public async Task Publish_WhenGridTooSmall_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Grid, "NINE_BOX", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Grid1x2, Ct);

        await Should.ThrowAsync<InvalidConfigPayloadException>(() => admin.PublishAsync(versionId, Ct));
    }

    [Fact]
    public async Task Publish_WhenGridBoundToUnpublishedScale_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var scaleDefinition = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        var draftScaleId = await admin.CreateDraftAsync(scaleDefinition, Payloads.Scale5, Ct);
        var gridDefinition = await admin.CreateDefinitionAsync(ConfigType.Grid, "NINE_BOX", "x", Ct);
        var gridPayload = Payloads.Grid2x2.Replace("\"mode\":\"Calculated\"", $"\"mode\":\"Calculated\",\"columnScaleVersionId\":\"{draftScaleId}\"", StringComparison.Ordinal);
        var gridId = await admin.CreateDraftAsync(gridDefinition, gridPayload, Ct);

        await Should.ThrowAsync<InvalidConfigPayloadException>(() => admin.PublishAsync(gridId, Ct));
    }

    [Fact]
    public async Task Publish_WhenAlreadyPublished_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var (_, versionId) = await PublishedScaleAsync(admin);

        await Should.ThrowAsync<InvalidConfigTransitionException>(() => admin.PublishAsync(versionId, Ct));
    }

    [Fact]
    public async Task UpdateDraft_WhenPublished_Throws()
    {
        await using var context = db.NewContext();
        var admin = db.NewAdmin(context);
        var (_, versionId) = await PublishedScaleAsync(admin);

        await Should.ThrowAsync<ConfigVersionLockedException>(() => admin.UpdateDraftAsync(versionId, Payloads.Scale5, Ct));
    }

    [Fact]
    public async Task UpdateDraft_WhenUnknownId_ThrowsNotFound()
    {
        await using var context = db.NewContext();

        await Should.ThrowAsync<ConfigNotFoundException>(
            () => db.NewAdmin(context).UpdateDraftAsync(Guid.NewGuid(), Payloads.Scale5, Ct));
    }

    [Fact]
    public async Task UpdateDraft_WhenChangedByAnotherUserFirst_ThrowsConflict()
    {
        await using var setup = db.NewContext();
        var admin = db.NewAdmin(setup);
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, "PERF_5", "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);

        await using var first = db.NewContext();
        await using var second = db.NewContext();
        var loadedByFirst = await first.Versions.SingleAsync(v => v.Id == versionId, Ct);
        var loadedBySecond = await second.Versions.SingleAsync(v => v.Id == versionId, Ct);
        loadedByFirst.UpdatePayload(Payloads.Scale5.Replace("Poor", "Weak", StringComparison.Ordinal));
        await first.SaveChangesAsync(Ct);
        loadedBySecond.UpdatePayload(Payloads.Scale5.Replace("Poor", "Bad", StringComparison.Ordinal));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    internal static async Task<(Guid DefinitionId, Guid VersionId)> PublishedScaleAsync(ConfigAdminService admin, string code = "PERF_5")
    {
        var definitionId = await admin.CreateDefinitionAsync(ConfigType.Scale, code, "x", Ct);
        var versionId = await admin.CreateDraftAsync(definitionId, Payloads.Scale5, Ct);
        await admin.PublishAsync(versionId, Ct);
        return (definitionId, versionId);
    }
}
