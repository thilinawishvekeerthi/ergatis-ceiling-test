using Talent.Shared.Errors;

namespace Talent.Config.Versions;

internal sealed class ConfigVersionLockedException(Guid versionId)
    : TalentProblemException(409, $"Config version {versionId} is not a draft and cannot be edited. Create a new version instead.");

internal sealed class InvalidConfigTransitionException(string action, string from)
    : TalentProblemException(409, $"Cannot {action} a config version that is {from}.");
