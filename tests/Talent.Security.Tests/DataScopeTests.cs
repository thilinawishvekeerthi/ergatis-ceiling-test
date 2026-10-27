using Talent.Security.Contracts;
using Talent.Security.Scopes;

namespace Talent.Security.Tests;

public sealed class DataScopeTests
{
    private sealed record Row(Guid EmployeeId, string Name);

    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Report = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();
    private static readonly Row[] Rows = [new(Me, "me"), new(Report, "report"), new(Stranger, "stranger")];

    private sealed class FakeMembers(params Guid[] reports) : IScopeMemberSource
    {
        public Task<IReadOnlyCollection<Guid>> GetEmployeeIdsAsync(DataScope scope, Guid caller, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(scope == DataScope.DirectReports ? reports : []);
    }

    private static AccessGrant Grant(params DataScope[] scopes) => new(scopes.ToHashSet(), FieldClass.Internal, false);

    private static SecurityContext User(Guid? employee) => SecurityContext.ForTest("u1", employee, ["R"]);

    private static async Task<string[]> Visible(IScopeMemberSource members, SecurityContext user, params DataScope[] scopes)
    {
        var service = new DataScopeService(user, members);
        var filter = await service.FilterAsync<Row>(Grant(scopes), r => r.EmployeeId, TestContext.Current.CancellationToken);
        return [.. Rows.AsQueryable().Where(filter).Select(r => r.Name)];
    }

    [Fact]
    public async Task Filter_ForAll_ReturnsEveryone() =>
        (await Visible(new FakeMembers(), User(Me), DataScope.All)).ShouldBe(["me", "report", "stranger"]);

    [Fact]
    public async Task Filter_ForSelf_ReturnsOnlyOwnRows() =>
        (await Visible(new FakeMembers(), User(Me), DataScope.Self)).ShouldBe(["me"]);

    [Fact]
    public async Task Filter_ForDirectReports_ReturnsReportsNotSelf() =>
        (await Visible(new FakeMembers(Report), User(Me), DataScope.DirectReports)).ShouldBe(["report"]);

    [Fact]
    public async Task Filter_ForSelfAndReports_ReturnsUnion() =>
        (await Visible(new FakeMembers(Report), User(Me), DataScope.Self, DataScope.DirectReports)).ShouldBe(["me", "report"]);

    [Theory]
    [InlineData(DataScope.DirectReports)]
    [InlineData(DataScope.DeptTree)]
    [InlineData(DataScope.CycleAssigned)]
    public async Task Filter_WhenNoModuleSuppliesMembers_ReachesNobody(DataScope scope) =>
        (await Visible(new EmptyScopeMemberSource(), User(Me), scope)).ShouldBeEmpty();

    [Fact]
    public async Task Filter_WhenCallerHasNoEmployee_ReachesNobody() =>
        (await Visible(new FakeMembers(Report), User(null), DataScope.Self, DataScope.DirectReports)).ShouldBeEmpty();

    [Fact]
    public async Task Filter_WhenGrantHasNoScopes_ReachesNobody() =>
        (await Visible(new FakeMembers(Report), User(Me))).ShouldBeEmpty();

    [Fact]
    public async Task Contains_ChecksOneRecordAgainstTheSameRules()
    {
        var service = new DataScopeService(User(Me), new FakeMembers(Report));

        (await service.ContainsAsync(Grant(DataScope.DirectReports), Report, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await service.ContainsAsync(Grant(DataScope.DirectReports), Me, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await service.ContainsAsync(Grant(DataScope.All), Stranger, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
}
