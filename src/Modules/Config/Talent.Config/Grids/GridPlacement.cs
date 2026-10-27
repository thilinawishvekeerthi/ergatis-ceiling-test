using Talent.Config.Contracts;

namespace Talent.Config.Grids;

/// <summary>
/// Where one person sits on a grid (ADR-03). Keeps the proposed cell next to the final cell,
/// so an override is always visible and always has an author and a reason.
/// </summary>
internal sealed class GridPlacement
{
    private GridPlacement(
        PlacementMode mode, GridCell? proposedCell, GridCell finalCell, string? overriddenBy, string? reason)
    {
        Mode = mode;
        ProposedCell = proposedCell;
        FinalCell = finalCell;
        OverriddenBy = overriddenBy;
        Reason = reason;
    }

    public PlacementMode Mode { get; }
    public GridCell? ProposedCell { get; }
    public GridCell FinalCell { get; }
    public string? OverriddenBy { get; }
    public string? Reason { get; }
    public bool IsOverridden => OverriddenBy is not null;

    public static GridPlacement Manual(GridCell cell, string placedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placedBy);
        return new GridPlacement(PlacementMode.Manual, null, cell, null, null);
    }

    public static GridPlacement Calculated(GridCell proposed) =>
        new(PlacementMode.Calculated, proposed, proposed, null, null);

    public static GridPlacement CalculatedWithOverride(GridCell proposed) =>
        new(PlacementMode.CalculatedWithOverride, proposed, proposed, null, null);

    public GridPlacement Override(GridCell cell, string overriddenBy, string reason)
    {
        if (Mode != PlacementMode.CalculatedWithOverride)
        {
            throw new OverrideNotAllowedException(Mode);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new OverrideReasonRequiredException();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(overriddenBy);
        return new GridPlacement(Mode, ProposedCell, cell, overriddenBy, reason);
    }
}
