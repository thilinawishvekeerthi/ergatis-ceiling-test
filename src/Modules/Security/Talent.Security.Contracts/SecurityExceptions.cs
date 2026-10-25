using Talent.Shared.Errors;

namespace Talent.Security.Contracts;

public sealed class NotAuthenticatedException()
    : TalentProblemException(401, "Sign in to continue.");

public sealed class ForbiddenException(string message)
    : TalentProblemException(403, message);
