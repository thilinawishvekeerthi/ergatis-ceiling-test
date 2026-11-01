using Talent.Career.Paths;

namespace Talent.Career.Tests;

public sealed class CareerGraphTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid();

    [Fact]
    public void Reachable_ReturnsFewestStepsToEachJob()
    {
        // A -> B -> C -> D and a shortcut A -> C
        var edges = new (Guid, Guid)[] { (A, B), (B, C), (C, D), (A, C) };

        var steps = CareerGraph.Reachable(edges, A, 10);

        steps.ShouldBe(new Dictionary<Guid, int> { [B] = 1, [C] = 1, [D] = 2 }, ignoreOrder: true);
    }

    [Fact]
    public void Reachable_StopsAtMaxSteps()
    {
        var edges = new (Guid, Guid)[] { (A, B), (B, C), (C, D) };

        CareerGraph.Reachable(edges, A, 2).Keys.ShouldBe([B, C], ignoreOrder: true);
    }

    [Fact]
    public void Reachable_FromJobWithNoPaths_IsEmpty() =>
        CareerGraph.Reachable([(A, B)], D, 5).ShouldBeEmpty();

    [Fact]
    public void Create_WhenJobsDiffer_Succeeds()
    {
        var path = CareerPath.Create(A, B, []);

        (path.FromJobId, path.ToJobId).ShouldBe((A, B));
    }

    [Fact]
    public void Create_WhenToItself_Throws() =>
        Should.Throw<InvalidCareerException>(() => CareerPath.Create(A, A, []));

    [Fact]
    public void Create_WhenAlreadyExists_Throws() =>
        Should.Throw<CareerConflictException>(() => CareerPath.Create(A, B, [(A, B)]));

    [Fact]
    public void Create_WhenItWouldMakeALoop_Throws()
    {
        // B -> C -> A exists, so A -> B would close the loop
        var edges = new (Guid, Guid)[] { (B, C), (C, A) };

        Should.Throw<CareerConflictException>(() => CareerPath.Create(A, B, edges));
    }

    [Fact]
    public void Create_AllowsTwoRoutesToTheSameJob() =>
        Should.NotThrow(() => CareerPath.Create(A, C, [(A, B), (B, C)]));
}
