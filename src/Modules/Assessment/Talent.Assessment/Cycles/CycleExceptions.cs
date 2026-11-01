using Talent.Shared.Errors;

namespace Talent.Assessment.Cycles;

internal sealed class AssessmentNotFoundException(string what, Guid id)
    : TalentProblemException(404, $"{what} {id} was not found.");

internal sealed class AssessmentConflictException(string message) : TalentProblemException(409, message);

internal sealed class InvalidCycleTransitionException(string action, string status)
    : TalentProblemException(409, $"Cannot {action} a cycle that is {status}.");

internal sealed class InvalidReviewException(string message) : TalentProblemException(400, message);
