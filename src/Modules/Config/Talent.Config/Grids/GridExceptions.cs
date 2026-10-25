namespace Talent.Config.Grids;

internal sealed class InvalidGridException(string message) : InvalidOperationException(message);

internal sealed class OverrideReasonRequiredException()
    : InvalidOperationException("An override needs a reason.");

internal sealed class OverrideNotAllowedException(Contracts.PlacementMode mode)
    : InvalidOperationException($"Placement mode {mode} does not allow an override.");
