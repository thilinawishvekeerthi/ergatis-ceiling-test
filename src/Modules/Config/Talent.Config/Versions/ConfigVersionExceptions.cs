namespace Talent.Config.Versions;

internal sealed class ConfigVersionLockedException(Guid versionId)
    : InvalidOperationException($"Config version {versionId} is not a draft and cannot be edited. Create a new version instead.");

internal sealed class InvalidConfigTransitionException(string action, string from)
    : InvalidOperationException($"Cannot {action} a config version that is {from}.");
