using Talent.Integration.Adapters;
using Talent.Integration.Contracts;

namespace Talent.Integration.Tests;

/// <summary>The same behaviour set every adapter (mock, DbView, Rest) must pass (tdd-and-testing skill).</summary>
public abstract class SourceAdapterContract<T>
    where T : class
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected abstract ISourceAdapter<T> Create(int seed, int employees, DateTimeOffset baseTime);

    private ISourceAdapter<T> New() => Create(7, 25, new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));

    private static async Task<List<SourceRecord<T>>> AllAsync(ISourceAdapter<T> adapter, DateTimeOffset since)
    {
        var list = new List<SourceRecord<T>>();
        await foreach (var r in adapter.FetchChangesAsync(since, Ct))
        {
            list.Add(r);
        }

        return list;
    }

    [Fact]
    public void System_IsCoreHr() => New().System.ShouldBe("CORE_HR");

    [Fact]
    public async Task FetchChanges_FromBeginningOfTime_ReturnsEverythingWithUniqueIds()
    {
        var all = await AllAsync(New(), DateTimeOffset.MinValue);

        all.ShouldNotBeEmpty();
        all.Select(r => r.ExternalId).Distinct().Count().ShouldBe(all.Count);
    }

    [Fact]
    public async Task FetchChanges_WhenSinceIsAfterLastChange_ReturnsNothing()
    {
        var all = await AllAsync(New(), DateTimeOffset.MinValue);
        var latest = all.Max(r => r.ModifiedAt);

        (await AllAsync(New(), latest)).ShouldBeEmpty();
    }

    [Fact]
    public async Task FetchOne_WhenKnown_ReturnsRecord()
    {
        var adapter = New();
        var first = (await AllAsync(adapter, DateTimeOffset.MinValue))[0];

        var one = await adapter.FetchOneAsync(first.ExternalId, Ct);

        one.ShouldNotBeNull().ExternalId.ShouldBe(first.ExternalId);
    }

    [Fact]
    public async Task FetchOne_WhenUnknown_ReturnsNull() =>
        (await New().FetchOneAsync("NOPE-404", Ct)).ShouldBeNull();

    [Fact]
    public async Task FetchChanges_SameSeed_IsDeterministic()
    {
        var a = await AllAsync(New(), DateTimeOffset.MinValue);
        var b = await AllAsync(New(), DateTimeOffset.MinValue);

        a.Select(r => r.ExternalId).ShouldBe(b.Select(r => r.ExternalId));
    }
}

public sealed class MockEmployeeAdapterTests : SourceAdapterContract<EmployeeSource>
{
    protected override ISourceAdapter<EmployeeSource> Create(int seed, int employees, DateTimeOffset baseTime) =>
        new MockEmployeeAdapter(new MockSourceData(seed, employees, baseTime));

    [Fact]
    public async Task FetchChanges_ReturnsEmployeesThatPassValidation()
    {
        await foreach (var r in Create(1, 30, DateTimeOffset.UnixEpoch).FetchChangesAsync(DateTimeOffset.MinValue, TestContext.Current.CancellationToken))
        {
            SourceValidator.Validate(r.Payload).ShouldBeNull();
        }
    }

    [Fact]
    public async Task FetchChanges_AfterUpdate_ReturnsOnlyChangedRecord()
    {
        var data = new MockSourceData(3, 10, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var adapter = new MockEmployeeAdapter(data);
        var changedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var first = data.Employees[0];
        data.Upsert(first.Payload with { LastName = "Renamed" }, changedAt);

        var changes = new List<SourceRecord<EmployeeSource>>();
        await foreach (var r in adapter.FetchChangesAsync(changedAt.AddDays(-1), TestContext.Current.CancellationToken))
        {
            changes.Add(r);
        }

        changes.Count.ShouldBe(1);
        changes[0].Payload.LastName.ShouldBe("Renamed");
    }
}

public sealed class MockOrgUnitAdapterTests : SourceAdapterContract<OrgUnitSource>
{
    protected override ISourceAdapter<OrgUnitSource> Create(int seed, int employees, DateTimeOffset baseTime) =>
        new MockOrgUnitAdapter(new MockSourceData(seed, employees, baseTime));
}

public sealed class MockPositionAdapterTests : SourceAdapterContract<PositionSource>
{
    protected override ISourceAdapter<PositionSource> Create(int seed, int employees, DateTimeOffset baseTime) =>
        new MockPositionAdapter(new MockSourceData(seed, employees, baseTime));
}
