namespace Talent.Shared.Errors;

/// <summary>
/// An expected failure with an HTTP meaning (validation 400, not authenticated 401, no permission 403, not found 404, conflict 409).
/// The host turns it into ProblemDetails. The message must be safe to show: no stack traces, no confidential data.
/// </summary>
public abstract class TalentProblemException(int statusCode, string message) : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;
}
