using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Defaults;
using Talent.Config.Grids;

namespace Talent.Config.IntegrationTests;

public sealed class ConfigDefaultsTests(ConfigDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task EnsureDefaults_WhenEmpty_PublishesDefaultScalesAndNineBox()
    {
        await using var context = db.NewContext();

        await ConfigDefaultsSeeder.EnsureAsync(context, db.Clock, Ct);

        var published = await context.Versions.Where(v => v.Status == ConfigVersionStatus.Published).CountAsync(Ct);
        published.ShouldBe(3);
        (await context.Definitions.Select(d => d.Code).OrderBy(c => c).ToListAsync(Ct))
            .ShouldBe([ConfigDefaultsSeeder.NineBoxCode, ConfigDefaultsSeeder.PerformanceScaleCode, ConfigDefaultsSeeder.PotentialScaleCode]);
    }

    [Fact]
    public async Task EnsureDefaults_WhenCalledTwice_DoesNotDuplicate()
    {
        await using var context = db.NewContext();

        await ConfigDefaultsSeeder.EnsureAsync(context, db.Clock, Ct);
        await ConfigDefaultsSeeder.EnsureAsync(context, db.Clock, Ct);

        (await context.Definitions.CountAsync(Ct)).ShouldBe(3);
        (await context.Versions.CountAsync(Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task DefaultNineBox_WhenPlacingScores_UsesThreeByThreeWithOverrideMode()
    {
        await using var context = db.NewContext();
        await ConfigDefaultsSeeder.EnsureAsync(context, db.Clock, Ct);
        var version = await context.Versions.SingleAsync(
            v => context.Definitions.Any(d => d.Id == v.DefinitionId && d.Code == ConfigDefaultsSeeder.NineBoxCode), Ct);

        var payload = GridPayload.Parse(version.PayloadJson);

        payload.Mode.ShouldBe(PlacementMode.CalculatedWithOverride);
        payload.Grid.Columns.ShouldBe(3);
        payload.Grid.Rows.ShouldBe(3);
        payload.Grid.Place(5m, 5m).ShouldBe(new GridCell(2, 2));
        payload.Grid.CellAt(new GridCell(2, 2)).Label.ShouldBe("Star");
        payload.Grid.Place(1m, 1m).ShouldBe(new GridCell(0, 0));
    }
}
