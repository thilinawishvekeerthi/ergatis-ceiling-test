namespace Talent.Config.Api;

/// <summary>
/// <c>/api/v1/config</c>. The group needs a signed-in user (401). Each call then checks the permission matrix in
/// <see cref="ConfigApiService"/> (403). State-changing calls also need the anti-forgery header (400), checked in the host pipeline.
/// </summary>
internal static class ConfigEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/config").RequireAuthorization();

        group.MapGet("/versions/{versionId:guid}", async (Guid versionId, ConfigApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetVersionAsync(versionId, ct)));

        group.MapGet("/{type}/{code}/latest", async (string type, string code, ConfigApiService api, CancellationToken ct) =>
            Results.Ok(await api.GetLatestAsync(type, code, ct)));

        group.MapPost("/{type}/{code}/drafts", async (string type, string code, CreateDraftRequest request, ConfigApiService api, CancellationToken ct) =>
        {
            var created = await api.CreateDraftAsync(type, code, request, ct);
            return Results.Created($"/api/v1/config/versions/{created.Id}", created);
        });

        group.MapPost("/{type}/{code}/versions", async (string type, string code, ConfigApiService api, CancellationToken ct) =>
        {
            var created = await api.NewVersionAsync(type, code, ct);
            return Results.Created($"/api/v1/config/versions/{created.Id}", created);
        });

        group.MapPut("/versions/{versionId:guid}/payload", async (Guid versionId, UpdatePayloadRequest request, ConfigApiService api, CancellationToken ct) =>
            Results.Ok(await api.UpdateDraftAsync(versionId, request, ct)));

        group.MapPost("/versions/{versionId:guid}/publish", async (Guid versionId, ConfigApiService api, CancellationToken ct) =>
            Results.Ok(await api.PublishAsync(versionId, ct)));

        group.MapPost("/versions/{versionId:guid}/lock", async (Guid versionId, ConfigApiService api, CancellationToken ct) =>
            Results.Ok(await api.LockAsync(versionId, ct)));
    }
}
