using Talent.Assessment.Contracts;
using Talent.Assessment.Cycles;

namespace Talent.Assessment.Tests;

public sealed class CycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static ReviewCycle NewCycle() =>
        ReviewCycle.Create("2026 annual", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_StartsAsDraftAndTrimsName()
    {
        var cycle = ReviewCycle.Create("  2026 annual ", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        cycle.Status.ShouldBe(CycleStatus.Draft);
        cycle.Name.ShouldBe("2026 annual");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhenNameBlank_Throws(string name) =>
        Should.Throw<InvalidReviewException>(() =>
            ReviewCycle.Create(name, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

    [Fact]
    public void Create_WhenEndBeforeStart_Throws() =>
        Should.Throw<InvalidReviewException>(() =>
            ReviewCycle.Create("X", new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

    [Fact]
    public void Lifecycle_MovesDraftOpenCalibrationClosed_AndRaisesOpenedEvent()
    {
        var cycle = NewCycle();

        cycle.Open(Now);
        cycle.Status.ShouldBe(CycleStatus.Open);
        cycle.OpenedAt.ShouldBe(Now);
        cycle.DomainEvents.OfType<ReviewCycleOpened>().Single().CycleId.ShouldBe(cycle.Id);

        cycle.StartCalibration();
        cycle.Status.ShouldBe(CycleStatus.Calibration);

        cycle.Close(Now.AddDays(30));
        cycle.Status.ShouldBe(CycleStatus.Closed);
        cycle.ClosedAt.ShouldBe(Now.AddDays(30));
    }

    [Fact]
    public void Open_WhenAlreadyOpen_Throws()
    {
        var cycle = NewCycle();
        cycle.Open(Now);

        Should.Throw<InvalidCycleTransitionException>(() => cycle.Open(Now));
    }

    [Fact]
    public void StartCalibration_WhenDraft_Throws() =>
        Should.Throw<InvalidCycleTransitionException>(() => NewCycle().StartCalibration());

    [Fact]
    public void Close_WhenStillOpen_Throws()
    {
        var cycle = NewCycle();
        cycle.Open(Now);

        Should.Throw<InvalidCycleTransitionException>(() => cycle.Close(Now));
    }

    [Fact]
    public void RequireOpenForRating_OnlyPassesWhileOpen()
    {
        var cycle = NewCycle();
        Should.Throw<InvalidCycleTransitionException>(cycle.RequireOpenForRating);

        cycle.Open(Now);
        Should.NotThrow(cycle.RequireOpenForRating);

        cycle.StartCalibration();
        Should.Throw<InvalidCycleTransitionException>(cycle.RequireOpenForRating);
    }

    [Fact]
    public void RequireCalibration_OnlyPassesInCalibration()
    {
        var cycle = NewCycle();
        cycle.Open(Now);
        Should.Throw<InvalidCycleTransitionException>(cycle.RequireCalibration);

        cycle.StartCalibration();
        Should.NotThrow(cycle.RequireCalibration);
    }

    [Fact]
    public void RequireOpenForAssignment_PassesInDraftAndOpenOnly()
    {
        var cycle = NewCycle();
        Should.NotThrow(cycle.RequireOpenForAssignment);
        cycle.Open(Now);
        Should.NotThrow(cycle.RequireOpenForAssignment);
        cycle.StartCalibration();
        Should.Throw<InvalidCycleTransitionException>(cycle.RequireOpenForAssignment);
    }
}
