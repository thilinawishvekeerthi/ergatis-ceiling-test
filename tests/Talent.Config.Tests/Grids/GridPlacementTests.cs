using Talent.Config.Grids;
using Talent.Config.Contracts;

namespace Talent.Config.Tests.Grids;

public sealed class GridPlacementTests
{
    private static readonly GridCell Proposed = new(1, 1);
    private static readonly GridCell Other = new(2, 0);

    [Fact]
    public void Calculated_WhenCreated_FinalCellEqualsProposed()
    {
        var placement = GridPlacement.Calculated(Proposed);

        placement.Mode.ShouldBe(PlacementMode.Calculated);
        placement.FinalCell.ShouldBe(Proposed);
        placement.IsOverridden.ShouldBeFalse();
    }

    [Fact]
    public void Manual_WhenCreated_HasNoProposedCell()
    {
        var placement = GridPlacement.Manual(Other, "board.head");

        placement.Mode.ShouldBe(PlacementMode.Manual);
        placement.ProposedCell.ShouldBeNull();
        placement.FinalCell.ShouldBe(Other);
    }

    [Fact]
    public void CalculatedWithOverride_WhenCreated_AllowsOverride()
    {
        var placement = GridPlacement.CalculatedWithOverride(Proposed);

        var overridden = placement.Override(Other, "board.head", "Strong project delivery");

        overridden.ProposedCell.ShouldBe(Proposed);
        overridden.FinalCell.ShouldBe(Other);
        overridden.OverriddenBy.ShouldBe("board.head");
        overridden.Reason.ShouldBe("Strong project delivery");
        overridden.IsOverridden.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Override_WhenReasonBlank_Throws(string reason)
    {
        var placement = GridPlacement.CalculatedWithOverride(Proposed);

        Should.Throw<OverrideReasonRequiredException>(() => placement.Override(Other, "x", reason));
    }

    [Fact]
    public void Override_WhenModeIsCalculated_Throws()
    {
        var placement = GridPlacement.Calculated(Proposed);

        Should.Throw<OverrideNotAllowedException>(() => placement.Override(Other, "x", "reason"));
    }

    [Fact]
    public void Override_WhenModeIsManual_Throws()
    {
        var placement = GridPlacement.Manual(Proposed, "x");

        Should.Throw<OverrideNotAllowedException>(() => placement.Override(Other, "x", "reason"));
    }

    [Fact]
    public void Override_DoesNotChangeOriginalPlacement()
    {
        var placement = GridPlacement.CalculatedWithOverride(Proposed);

        placement.Override(Other, "x", "reason");

        placement.FinalCell.ShouldBe(Proposed);
    }
}
