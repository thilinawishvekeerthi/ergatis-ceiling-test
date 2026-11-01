using Talent.Shared.Errors;

namespace Talent.Career;

internal sealed class CareerNotFoundException(string what, Guid id)
    : TalentProblemException(404, $"{what} {id} was not found.");

internal sealed class CareerConflictException(string message) : TalentProblemException(409, message);

internal sealed class InvalidCareerException(string message) : TalentProblemException(400, message);
