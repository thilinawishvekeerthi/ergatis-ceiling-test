using Talent.Shared.Errors;

namespace Talent.Config.Grids;

internal sealed class InvalidGridException(string message) : TalentProblemException(400, message);

internal sealed class OverrideReasonRequiredException()
    : TalentProblemException(400, "An override needs a reason.");

internal sealed class OverrideNotAllowedException(Contracts.PlacementMode mode)
    : TalentProblemException(400, $"Placement mode {mode} does not allow an override.");
