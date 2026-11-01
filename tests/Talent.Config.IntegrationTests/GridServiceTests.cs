using Talent.Config.Contracts;
using Talent.Config.Defaults;
using Talent.Config.Versions;

namespace Talent.Config.IntegrationTests;

public sealed class GridServiceTests(ConfigDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<(GridService Service, Guid GridVersionId, Guid ScaleVersionId)> SeedAsync()
    {
        var context = db.NewContext();
        await ConfigDefaultsSeeder.EnsureAsync(context, db.Clock, Ct);
        var reader = new ConfigVersionReader(context, db.Clock, new Talent.Shared.Security.SystemCurrentUser());
        var grid = await reader.GetLatestPublishedAsync(ConfigType.Grid, ConfigDefaultsSeeder.NineBoxCode, Ct);
        var scale = await reader.GetLatestPublishedAsync(ConfigType.Scale, ConfigDefaultsSeeder.PerformanceScaleCode, Ct);
        return (new GridService(context), grid!.Id, scale!.Id);
    }

    [Fact]
    public async Task GetLayoutAsync_ForDefaultNineBox_ReturnsNineLabelledCells()
    {
        var (service, gridId, _) = await SeedAsync();

        var layout = await service.GetLayoutAsync(gridId, Ct);

        layout.ShouldNotBeNull();
        layout.Columns.ShouldBe(3);
        layout.Rows.ShouldBe(3);
        layout.Mode.ShouldBe(PlacementMode.CalculatedWithOverride);
        layout.Cells.Count.ShouldBe(9);
        layout.Cells.Single(c => c is { Column: 2, Row: 2 }).Label.ShouldBe("Star");
    }

    [Fact]
    public async Task GetLayoutAsync_WhenVersionIsNotAGrid_ReturnsNull()
    {
        var (service, _, scaleId) = await SeedAsync();

        (await service.GetLayoutAsync(scaleId, Ct)).ShouldBeNull();
        (await service.GetLayoutAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task PlaceAsync_UsesColumnAndRowScores()
    {
        var (service, gridId, _) = await SeedAsync();

        (await service.PlaceAsync(gridId, 5m, 3m, Ct)).ShouldBe(new GridCell(2, 2));
        (await service.PlaceAsync(gridId, 1m, 1m, Ct)).ShouldBe(new GridCell(0, 0));
        (await service.PlaceAsync(gridId, 3m, 2m, Ct)).ShouldBe(new GridCell(1, 1));
    }

    [Fact]
    public async Task PlaceAsync_WhenVersionUnknown_ThrowsNotFound()
    {
        var (service, _, _) = await SeedAsync();

        await Should.ThrowAsync<ConfigNotFoundException>(() => service.PlaceAsync(Guid.NewGuid(), 3m, 2m, Ct));
    }
}
