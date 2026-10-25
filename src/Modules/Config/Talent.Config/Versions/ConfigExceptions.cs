namespace Talent.Config.Versions;

internal sealed class ConfigNotFoundException(string what, Guid id)
    : InvalidOperationException($"{what} {id} was not found.");

internal sealed class ConfigConflictException(string message) : InvalidOperationException(message);
