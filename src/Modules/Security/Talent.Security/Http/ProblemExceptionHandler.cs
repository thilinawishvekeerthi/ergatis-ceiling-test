using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Talent.Shared.Errors;

namespace Talent.Security.Http;

/// <summary>
/// The single global exception handler. Expected failures become ProblemDetails (RFC 9457) with their own status.
/// Anything else is a 500 with a fixed message: no stack trace, no exception text, no confidential data.
/// </summary>
internal sealed partial class ProblemExceptionHandler(IProblemDetailsService problems, ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ProblemDetails details;
        switch (exception)
        {
            case ValidationFailedException validation:
                details = new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
                {
                    Status = validation.StatusCode,
                    Title = Title(validation.StatusCode),
                };
                break;
            case TalentProblemException expected:
                details = new ProblemDetails { Status = expected.StatusCode, Title = Title(expected.StatusCode), Detail = expected.Message };
                break;
            case BadHttpRequestException bad:
                details = new ProblemDetails { Status = bad.StatusCode, Title = Title(bad.StatusCode), Detail = "The request could not be read." };
                break;
            case OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested:
                return true;
            default:
                LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path.Value);
                details = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = Title(500), Detail = "Something went wrong. Try again later." };
                break;
        }

        httpContext.Response.StatusCode = details.Status ?? StatusCodes.Status500InternalServerError;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = details, Exception = null });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled error on {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string? path);

    private static string Title(int status) => status switch
    {
        400 => "The request is not valid.",
        401 => "Sign in to continue.",
        403 => "You are not allowed to do this.",
        404 => "Not found.",
        409 => "Conflict.",
        _ => "Server error.",
    };
}
