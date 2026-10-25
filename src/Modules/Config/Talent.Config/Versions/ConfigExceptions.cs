using Talent.Shared.Errors;

namespace Talent.Config.Versions;

internal sealed class ConfigNotFoundException(string what, Guid id)
    : TalentProblemException(404, $"{what} {id} was not found.");

internal sealed class ConfigConflictException(string message) : TalentProblemException(409, message);
