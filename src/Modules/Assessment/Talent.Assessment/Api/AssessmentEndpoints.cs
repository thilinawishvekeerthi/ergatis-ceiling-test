namespace Talent.Assessment.Api;

/// <summary>
/// <c>/api/v1/assessment</c>. The group needs a signed-in user (401). Each call then checks the permission matrix and data scope
/// in <see cref="AssessmentApiService"/> (403). State-changing calls also need the anti-forgery header (400), checked in the host pipeline.
/// </summary>
internal static class AssessmentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assessment").RequireAuthorization();

        group.MapGet("/cycles", async (AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.ListCyclesAsync(ct)));

        group.MapPost("/cycles", async (CreateCycleRequest request, AssessmentApiService api, CancellationToken ct) =>
        {
            var created = await api.CreateCycleAsync(request, ct);
            return Results.Created($"/api/v1/assessment/cycles/{created.Id}", created);
        });

        group.MapGet("/cycles/{cycleId:guid}", async (Guid cycleId, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetCycleAsync(cycleId, ct)));

        group.MapPost("/cycles/{cycleId:guid}/open", async (Guid cycleId, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.OpenCycleAsync(cycleId, ct)));

        group.MapPost("/cycles/{cycleId:guid}/calibration", async (Guid cycleId, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.StartCalibrationAsync(cycleId, ct)));

        group.MapPost("/cycles/{cycleId:guid}/close", async (Guid cycleId, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.CloseCycleAsync(cycleId, ct)));

        group.MapGet("/cycles/{cycleId:guid}/reviews", async (Guid cycleId, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.ListReviewsAsync(cycleId, ct)));

        group.MapPost("/cycles/{cycleId:guid}/reviews", async (Guid cycleId, AssignReviewRequest request, AssessmentApiService api, CancellationToken ct) =>
        {
            var created = await api.AssignAsync(cycleId, request, ct);
            return Results.Created($"/api/v1/assessment/reviews/{created.Id}", created);
        });

        group.MapGet("/cycles/{cycleId:guid}/board", async (Guid cycleId, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetBoardAsync(cycleId, ct)));

        group.MapGet("/reviews/{reviewId:guid}", async (Guid reviewId, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetReviewAsync(reviewId, ct)));

        group.MapPut("/reviews/{reviewId:guid}/rating", async (Guid reviewId, RateRequest request, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.RateAsync(reviewId, request, ct)));

        group.MapPost("/reviews/{reviewId:guid}/calibrate", async (Guid reviewId, CalibrateRequest request, AssessmentApiService api, CancellationToken ct) =>
            Results.Ok(await api.CalibrateAsync(reviewId, request, ct)));
    }
}
