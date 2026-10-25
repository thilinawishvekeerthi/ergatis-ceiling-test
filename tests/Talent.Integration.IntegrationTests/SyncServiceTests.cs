using Microsoft.EntityFrameworkCore;
using Talent.Integration.Adapters;
using Talent.Integration.Contracts;

namespace Talent.Integration.IntegrationTests;

public sealed class SyncServiceTests(IntegrationDatabaseFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly DateTimeOffset _t0 = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        db.Clock.Advance(TimeSpan.FromDays(10));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static SourceRecord<EmployeeSource> Emp(string number, DateTimeOffset at, string? manager = null, string? email = "x@example.com", string? org = null) =>
        new(number, new EmployeeSource(number, "First" + number, "Last" + number, email, org, null, manager, new DateOnly(2020, 1, 1), "Active"), at);

    private static SourceRecord<OrgUnitSource> Org(string code, DateTimeOffset at, string? parent = null) =>
        new(code, new OrgUnitSource(code, "Unit " + code, parent, true), at);

    private sealed class Sources
    {
        public ScriptedAdapter<OrgUnitSource> Orgs { get; } = new();
        public ScriptedAdapter<PositionSource> Positions { get; } = new();
        public ScriptedAdapter<EmployeeSource> Employees { get; } = new();
    }

    [Fact]
    public async Task RunDelta_FirstRun_LoadsCanonicalTablesWithResolvedReferences()
    {
        var data = new MockSourceData(5, 40, _t0);
        await using var context = db.NewContext();

        var result = await db.NewSync(context, data).RunDeltaAsync(Ct);

        result.TotalDeadLettered.ShouldBe(0);
        (await context.OrgUnits.CountAsync(Ct)).ShouldBe(data.OrgUnits.Count);
        (await context.Positions.CountAsync(Ct)).ShouldBe(data.Positions.Count);
        (await context.Employees.CountAsync(Ct)).ShouldBe(40);
        (await context.Employees.CountAsync(e => e.ManagerId != null, Ct)).ShouldBe(39);
        (await context.IdMap.CountAsync(Ct)).ShouldBe(data.OrgUnits.Count + data.Positions.Count + 40);
    }

    [Fact]
    public async Task RunDelta_RunTwiceWithNoChanges_IsIdempotent()
    {
        var data = new MockSourceData(5, 25, _t0);
        await using var context = db.NewContext();
        await db.NewSync(context, data).RunDeltaAsync(Ct);
        var ids = await context.Employees.OrderBy(e => e.EmployeeNumber).Select(e => e.Id).ToListAsync(Ct);

        // Force a full re-read: forget the watermark.
        await context.SyncStates.ExecuteDeleteAsync(Ct);
        db.Clock.Advance(TimeSpan.FromDays(1));
        var second = await db.NewSync(context, data).RunDeltaAsync(Ct);

        second.TotalDeadLettered.ShouldBe(0);
        (await context.Employees.OrderBy(e => e.EmployeeNumber).Select(e => e.Id).ToListAsync(Ct)).ShouldBe(ids);
        (await context.Employees.CountAsync(Ct)).ShouldBe(25);
        (await context.IdMap.CountAsync(m => m.Entity == "Employee", Ct)).ShouldBe(25);
    }

    [Fact]
    public async Task RunDelta_WhenSourceRecordChanges_UpdatesSameCanonicalRow()
    {
        var data = new MockSourceData(5, 10, _t0);
        await using var context = db.NewContext();
        await db.NewSync(context, data).RunDeltaAsync(Ct);
        var original = data.Employees[3];
        var idBefore = (await context.Employees.SingleAsync(e => e.EmployeeNumber == original.ExternalId, Ct)).Id;

        db.Clock.Advance(TimeSpan.FromDays(1));
        data.Upsert(original.Payload with { LastName = "Changed" }, db.Clock.GetUtcNow());
        var result = await db.NewSync(context, data).RunDeltaAsync(Ct);

        result.Entities.Single(e => e.Entity == "Employee").Fetched.ShouldBe(1);
        await using var fresh = db.NewContext();
        var row = await fresh.Employees.SingleAsync(e => e.EmployeeNumber == original.ExternalId, Ct);
        row.Id.ShouldBe(idBefore);
        row.LastName.ShouldBe("Changed");
        (await fresh.Employees.CountAsync(Ct)).ShouldBe(10);
    }

    [Fact]
    public async Task RunDelta_WhenOlderVersionArrivesLate_DoesNotOverwriteNewerData()
    {
        var s = new Sources();
        s.Employees.Records.Add(Emp("E1", _t0.AddDays(2)) with { Payload = Emp("E1", _t0).Payload with { LastName = "New" } });
        await using var context = db.NewContext();
        await db.NewSync(context, s.Orgs, s.Positions, s.Employees).RunDeltaAsync(Ct);

        // A stale copy of the same record shows up after the watermark moved.
        s.Employees.Records.Clear();
        s.Employees.Records.Add(Emp("E1", _t0.AddDays(1)) with { Payload = Emp("E1", _t0).Payload with { LastName = "Old" } });
        await context.SyncStates.ExecuteDeleteAsync(Ct);
        await db.NewSync(context, s.Orgs, s.Positions, s.Employees).RunDeltaAsync(Ct);

        (await context.Employees.AsNoTracking().SingleAsync(Ct)).LastName.ShouldBe("New");
    }

    [Fact]
    public async Task RunDelta_WhenChildArrivesBeforeParent_ResolvesInLaterPass()
    {
        var s = new Sources();
        s.Orgs.Records.Add(Org("CHILD", _t0, parent: "PARENT"));
        s.Orgs.Records.Add(Org("PARENT", _t0));
        s.Employees.Records.Add(Emp("E2", _t0, manager: "E1"));
        s.Employees.Records.Add(Emp("E1", _t0));
        await using var context = db.NewContext();

        var result = await db.NewSync(context, s.Orgs, s.Positions, s.Employees).RunDeltaAsync(Ct);

        result.TotalDeadLettered.ShouldBe(0);
        var child = await context.OrgUnits.SingleAsync(o => o.Code == "CHILD", Ct);
        child.ParentId.ShouldBe((await context.OrgUnits.SingleAsync(o => o.Code == "PARENT", Ct)).Id);
        (await context.Employees.SingleAsync(e => e.EmployeeNumber == "E2", Ct)).ManagerId
            .ShouldBe((await context.Employees.SingleAsync(e => e.EmployeeNumber == "E1", Ct)).Id);
    }

    [Fact]
    public async Task RunDelta_WhenRowsAreBad_DeadLettersThemAndKeepsGoing()
    {
        var s = new Sources();
        s.Employees.Records.Add(Emp("GOOD1", _t0));
        s.Employees.Records.Add(Emp("BAD-EMAIL", _t0, email: "nope"));
        s.Employees.Records.Add(Emp("ORPHAN", _t0, manager: "MISSING"));
        s.Employees.Records.Add(Emp("GOOD2", _t0));
        s.Employees.Records.Add(Emp("BLANK", _t0) with { Payload = Emp("BLANK", _t0).Payload with { FirstName = "" } });
        await using var context = db.NewContext();

        var result = await db.NewSync(context, s.Orgs, s.Positions, s.Employees).RunDeltaAsync(Ct);

        result.Entities.Single(e => e.Entity == "Employee").DeadLettered.ShouldBe(3);
        (await context.Employees.Select(e => e.EmployeeNumber).OrderBy(x => x).ToListAsync(Ct)).ShouldBe(["GOOD1", "GOOD2"]);
        var letters = await context.DeadLetters.OrderBy(d => d.ExternalId).ToListAsync(Ct);
        letters.Select(l => l.ExternalId).ShouldBe(["BAD-EMAIL", "BLANK", "ORPHAN"]);
        letters.ShouldAllBe(l => l.Error != "" && l.PayloadJson.StartsWith('{') && l.Attempts == 1 && l.ResolvedAt == null);
        // Rejected rows leave no id mapping behind.
        (await context.IdMap.Select(m => m.ExternalId).OrderBy(x => x).ToListAsync(Ct)).ShouldBe(["GOOD1", "GOOD2"]);
    }

    [Fact]
    public async Task RunDelta_WhenDatabaseRejectsRow_DeadLettersIt()
    {
        var s = new Sources();
        s.Employees.Records.Add(Emp("A", _t0));
        // Same employee number under a different key violates the unique index.
        s.Employees.Records.Add(Emp("B", _t0) with { Payload = Emp("A", _t0).Payload });
        await using var context = db.NewContext();

        var result = await db.NewSync(context, s.Orgs, s.Positions, s.Employees).RunDeltaAsync(Ct);

        result.Entities.Single(e => e.Entity == "Employee").DeadLettered.ShouldBe(1);
        (await context.Employees.CountAsync(Ct)).ShouldBe(1);
        (await context.DeadLetters.SingleAsync(Ct)).ExternalId.ShouldBe("B");
        (await context.IdMap.CountAsync(m => m.ExternalId == "B", Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task RunDelta_WhenSameRowFailsAgain_BumpsAttemptsOnOneOpenDeadLetter()
    {
        var s = new Sources();
        s.Employees.Records.Add(Emp("BAD", _t0, email: "nope"));
        await using var context = db.NewContext();
        var sync = db.NewSync(context, s.Orgs, s.Positions, s.Employees);

        await sync.RunDeltaAsync(Ct);
        db.Clock.Advance(TimeSpan.FromDays(1));
        await sync.RunDeltaAsync(Ct);

        var letter = await context.DeadLetters.AsNoTracking().SingleAsync(Ct);
        letter.Attempts.ShouldBe(2);
        letter.LastFailedAt.ShouldBeGreaterThan(letter.FirstFailedAt);
    }

    [Fact]
    public async Task RunDelta_WhenSourceIsFixed_RecoversDeadLetterOnNextRun()
    {
        var s = new Sources();
        s.Employees.Records.Add(Emp("FIXME", _t0, email: "nope"));
        await using var context = db.NewContext();
        var sync = db.NewSync(context, s.Orgs, s.Positions, s.Employees);
        await sync.RunDeltaAsync(Ct);

        // The source corrects the row but does not touch its modified time, so only the dead-letter retry can see it.
        s.Employees.Records[0] = Emp("FIXME", _t0);
        db.Clock.Advance(TimeSpan.FromDays(1));
        var result = await sync.RunDeltaAsync(Ct);

        result.Entities.Single(e => e.Entity == "Employee").Recovered.ShouldBe(1);
        (await context.Employees.CountAsync(e => e.EmployeeNumber == "FIXME", Ct)).ShouldBe(1);
        (await context.DeadLetters.AsNoTracking().SingleAsync(Ct)).ResolvedAt.ShouldNotBeNull();
        (await IntegrationDatabaseFixture.NewDeadLetterReader(db.NewContext()).ListOpenAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RunDelta_AfterSuccess_StoresWatermarkPerSystemAndEntity()
    {
        var data = new MockSourceData(5, 5, _t0);
        await using var context = db.NewContext();

        await db.NewSync(context, data).RunDeltaAsync(Ct);

        var states = await context.SyncStates.AsNoTracking().ToListAsync(Ct);
        states.Select(x => x.Entity).Order().ShouldBe(["Employee", "OrgUnit", "Position"]);
        states.ShouldAllBe(x => x.System == "CORE_HR" && x.LastSuccessAt == db.Clock.GetUtcNow());
    }

    [Fact]
    public async Task RunDelta_WhenSourceIsDown_ThrowsAndKeepsWatermark()
    {
        var s = new Sources();
        s.Employees.Records.Add(Emp("E1", _t0));
        await using var context = db.NewContext();
        await db.NewSync(context, s.Orgs, s.Positions, s.Employees).RunDeltaAsync(Ct);
        var before = (await context.SyncStates.AsNoTracking().SingleAsync(x => x.Entity == "Employee", Ct)).LastSuccessAt;

        s.Employees.Down = true;
        db.Clock.Advance(TimeSpan.FromDays(1));
        await Should.ThrowAsync<HttpRequestException>(() => db.NewSync(context, s.Orgs, s.Positions, s.Employees).RunDeltaAsync(Ct));

        (await context.SyncStates.AsNoTracking().SingleAsync(x => x.Entity == "Employee", Ct)).LastSuccessAt.ShouldBe(before);
        (await context.Employees.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task RunDelta_WhenTwoSyncsRunAtOnce_CreatesNoDuplicates()
    {
        var data = new MockSourceData(9, 60, _t0);
        await using var c1 = db.NewContext();
        await using var c2 = db.NewContext();

        // Whichever run loses a race on a record must neither fail nor duplicate it.
        var results = await Task.WhenAll(
            db.NewSync(c1, data).RunDeltaAsync(Ct),
            db.NewSync(c2, data).RunDeltaAsync(Ct));

        await using var check = db.NewContext();
        (await check.Employees.CountAsync(Ct)).ShouldBe(60);
        (await check.IdMap.CountAsync(m => m.Entity == "Employee", Ct)).ShouldBe(60);
        results.Sum(r => r.TotalDeadLettered).ShouldBe(0);
    }
}
