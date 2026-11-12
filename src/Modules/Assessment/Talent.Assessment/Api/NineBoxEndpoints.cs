using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Talent.Integration.Contracts;

namespace Talent.Assessment.Api;

/// <summary>
/// <c>/api/v1/assessment/nine-box</c>, as in <c>docs/api/nine-box.openapi.yaml</c>. The group needs a signed-in user (401);
/// <see cref="NineBoxService"/> checks the permission matrix (403). POST also needs the anti-forgery header.
/// </summary>
internal static class NineBoxEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assessment/nine-box").RequireAuthorization().WithTags("NineBox");

        group.MapGet("/grid", async (NineBoxService api, CancellationToken ct) =>
                Results.Ok(await api.GetGridAsync(ct)))
            .WithName("GetNineBoxGrid")
            .Produces<NineBoxGridResponse>();

        group.MapGet("/employees", async (int? page, int? pageSize, NineBoxService api, CancellationToken ct) =>
                Results.Ok(await api.ListEmployeesAsync(page ?? 1, pageSize ?? 50, ct)))
            .WithName("ListNineBoxEmployees")
            .Produces<PagedResult<EmployeePlacementRow>>();

        group.MapGet("/employees/{employeeId:guid}/evaluations", async (Guid employeeId, int? page, int? pageSize, NineBoxService api, CancellationToken ct) =>
                Results.Ok(await api.HistoryAsync(employeeId, page ?? 1, pageSize ?? 20, ct)))
            .WithName("ListNineBoxEvaluations")
            .Produces<PagedResult<EvaluationResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/evaluations", async (EvaluateRequest request, NineBoxService api, CancellationToken ct) =>
            {
                var created = await api.EvaluateAsync(request, ct);
                return Results.Created($"/api/v1/assessment/nine-box/employees/{created.EmployeeId}/evaluations", created);
            })
            .WithName("CreateNineBoxEvaluation")
            .Produces<EvaluationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
