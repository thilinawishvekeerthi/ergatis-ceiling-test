namespace Talent.Shared.Errors;

/// <summary>Input broke a rule at the edge (400). <see cref="Errors"/> maps a field name to its messages.</summary>
public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : TalentProblemException(400, "One or more fields are invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
