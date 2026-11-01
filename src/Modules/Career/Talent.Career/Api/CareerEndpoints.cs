namespace Talent.Career.Api;

/// <summary>
/// <c>/api/v1/career</c>. The group needs a signed-in user (401). Each call then checks the permission matrix and data scope
/// in <see cref="CareerApiService"/> (403). State-changing calls also need the anti-forgery header (400), checked in the host pipeline.
/// </summary>
internal static class CareerEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/career").RequireAuthorization();

        group.MapGet("/jobs", async (bool? includeInactive, CareerApiService api, CancellationToken ct) =>
            Results.Ok(await api.ListJobsAsync(includeInactive ?? false, ct)));

        group.MapGet("/jobs/{jobId:guid}", async (Guid jobId, CareerApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetJobAsync(jobId, ct)));

        group.MapPost("/jobs", async (JobRequest request, CareerApiService api, CancellationToken ct) =>
        {
            var created = await api.CreateJobAsync(request, ct);
            return Results.Created($"/api/v1/career/jobs/{created.Id}", created);
        });

        group.MapPut("/jobs/{jobId:guid}", async (Guid jobId, JobRequest request, CareerApiService api, CancellationToken ct) =>
            Results.Ok(await api.UpdateJobAsync(jobId, request, ct)));

        group.MapGet("/paths", async (CareerApiService api, CancellationToken ct) =>
            Results.Ok(await api.ListPathsAsync(ct)));

        group.MapPost("/paths", async (AddPathRequest request, CareerApiService api, CancellationToken ct) =>
        {
            var created = await api.AddPathAsync(request, ct);
            return Results.Created($"/api/v1/career/paths/{created.Id}", created);
        });

        group.MapDelete("/paths/{pathId:guid}", async (Guid pathId, CareerApiService api, CancellationToken ct) =>
        {
            await api.RemovePathAsync(pathId, ct);
            return Results.NoContent();
        });

        group.MapGet("/me/centre", async (CareerApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetMyCentreAsync(ct)));

        group.MapGet("/employees/{employeeId:guid}/centre", async (Guid employeeId, CareerApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetCentreAsync(employeeId, ct)));

        group.MapPut("/employees/{employeeId:guid}/profile", async (Guid employeeId, ProfileRequest request, CareerApiService api, CancellationToken ct) =>
            Results.Ok(await api.ReplaceProfileAsync(employeeId, request, ct)));
    }
}
