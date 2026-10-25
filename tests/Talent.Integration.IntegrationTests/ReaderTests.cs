using Microsoft.EntityFrameworkCore;
using Talent.Integration.Adapters;
using Talent.Integration.Contracts;

namespace Talent.Integration.IntegrationTests;

public sealed class ReaderTests(IntegrationDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset T0 = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        await using var context = db.NewContext();
        await db.NewSync(context, new MockSourceData(3, 30, T0)).RunDeltaAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FindByEmployeeNumber_WhenKnown_ReturnsCanonicalEmployee()
    {
        await using var context = db.NewContext();
        var reader = IntegrationDatabaseFixture.NewEmployeeReader(context);

        var found = await reader.FindByEmployeeNumberAsync("E00002", Ct);

        found.ShouldNotBeNull();
        (await reader.GetAsync(found.Id, Ct)).ShouldBe(found);
        found.ManagerId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Get_WhenUnknown_ReturnsNull()
    {
        await using var context = db.NewContext();

        (await IntegrationDatabaseFixture.NewEmployeeReader(context).GetAsync(Guid.NewGuid(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetDirectReports_ReturnsOnlyThatManagersReports()
    {
        await using var context = db.NewContext();
        var reader = IntegrationDatabaseFixture.NewEmployeeReader(context);
        var managerId = (await context.Employees.Where(e => e.ManagerId != null).GroupBy(e => e.ManagerId).OrderByDescending(g => g.Count()).Select(g => g.Key!.Value).FirstAsync(Ct));

        var reports = await reader.GetDirectReportsAsync(managerId, Ct);

        reports.ShouldNotBeEmpty();
        reports.ShouldAllBe(r => r.ManagerId == managerId);
        reports.Select(r => r.EmployeeNumber).ShouldBe(reports.Select(r => r.EmployeeNumber).Order());
    }

    [Fact]
    public async Task List_PagesInStableOrderAndCapsPageSize()
    {
        await using var context = db.NewContext();
        var reader = IntegrationDatabaseFixture.NewEmployeeReader(context);

        var p1 = await reader.ListAsync(null, 1, 10, Ct);
        var p2 = await reader.ListAsync(null, 2, 10, Ct);
        var huge = await reader.ListAsync(null, 1, 100_000, Ct);

        p1.TotalCount.ShouldBe(30);
        p1.Items.Count.ShouldBe(10);
        p1.Items.Concat(p2.Items).Select(e => e.EmployeeNumber).ShouldBe(Enumerable.Range(1, 20).Select(i => $"E{i:00000}"));
        huge.PageSize.ShouldBe(500);
    }

    [Fact]
    public async Task List_FilteredByOrgUnit_ReturnsOnlyThatUnit()
    {
        await using var context = db.NewContext();
        var unit = await context.Employees.Where(e => e.OrgUnitId != null).Select(e => e.OrgUnitId!.Value).FirstAsync(Ct);

        var page = await IntegrationDatabaseFixture.NewEmployeeReader(context).ListAsync(unit, 1, 50, Ct);

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(e => e.OrgUnitId == unit);
    }

    [Fact]
    public async Task GetMany_ReturnsRequestedEmployeesAndIgnoresUnknownIds()
    {
        await using var context = db.NewContext();
        var ids = await context.Employees.Select(e => e.Id).Take(3).ToListAsync(Ct);

        var many = await IntegrationDatabaseFixture.NewEmployeeReader(context).GetManyAsync([.. ids, Guid.NewGuid()], Ct);

        many.Select(e => e.Id).Order().ShouldBe(ids.Order());
    }

    [Fact]
    public async Task OrgUnitAndPositionReaders_ReturnSyncedData()
    {
        await using var context = db.NewContext();
        var units = await IntegrationDatabaseFixture.NewOrgUnitReader(context).ListAsync(Ct);
        var position = (await IntegrationDatabaseFixture.NewPositionReader(context).ListAsync(null, 1, 5, Ct)).Items[0];

        units.ShouldNotBeEmpty();
        units.Count(u => u.ParentId == null).ShouldBe(1);
        (await IntegrationDatabaseFixture.NewOrgUnitReader(context).GetAsync(units[0].Id, Ct)).ShouldBe(units[0]);
        (await IntegrationDatabaseFixture.NewPositionReader(context).GetAsync(position.Id, Ct)).ShouldBe(position);
    }
}
