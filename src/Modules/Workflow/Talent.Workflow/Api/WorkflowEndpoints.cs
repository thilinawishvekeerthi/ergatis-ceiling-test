using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Talent.Shared.Errors;
using Talent.Workflow.Contracts;

namespace Talent.Workflow.Api;

/// <summary>
/// <c>/api/v1/workflows</c>. Every call needs a signed-in user (401). Who may act is the workflow's own rule (403).
/// State-changing calls also need the anti-forgery header (400), checked in the host pipeline.
/// </summary>
internal static class WorkflowEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/workflows").RequireAuthorization().AddEndpointFilter(MapErrors);

        group.MapGet("/mine", async (WorkflowApi api, CancellationToken ct) => Results.Ok(await api.ListMineAsync(ct)));

        group.MapGet("/inbox", async (WorkflowApi api, CancellationToken ct) => Results.Ok(await api.GetInboxAsync(ct)));

        group.MapPost(string.Empty, async (StartWorkflowBody body, WorkflowApi api, CancellationToken ct) =>
        {
            var started = await api.StartAsync(body, ct);
            return Results.Created($"/api/v1/workflows/{started.Id}", started);
        });

        group.MapGet("/{instanceId:guid}", async (Guid instanceId, WorkflowApi api, CancellationToken ct) =>
            Results.Ok(await api.GetAsync(instanceId, ct)));

        group.MapPost("/{instanceId:guid}/actions", async (Guid instanceId, ActionBody body, WorkflowApi api, CancellationToken ct) =>
            Results.Ok(await api.ActAsync(instanceId, body, ct)));
    }

    /// <summary>Turns the engine's expected failures into ProblemDetails with the right status.</summary>
    private static async ValueTask<object?> MapErrors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (WorkflowException ex)
        {
            throw new WorkflowProblemException(ex);
        }
    }
}

internal sealed class WorkflowProblemException(WorkflowException inner) : TalentProblemException(
    inner.Kind switch
    {
        WorkflowErrorKind.NotFound => StatusCodes.Status404NotFound,
        WorkflowErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        WorkflowErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    },
    inner.Message);
