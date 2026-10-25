using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Talent.Config.Contracts;
using Talent.Config.Persistence;
using Talent.Config.Versions;
using Talent.Security.Contracts;
using Talent.Shared.Errors;

namespace Talent.Config.Api;

/// <summary>
/// The Config API rules: who may do what (permission matrix), then the admin operations, then masking.
/// Config objects are organisation-wide, so every call needs a grant with the All data scope.
/// Reads need View on <c>config:{Type}</c>, edits need Edit, publish and lock need Approve.
/// </summary>
internal sealed class ConfigApiService(
    ConfigDbContext db,
    ConfigAdminService admin,
    IAccessGuard guard,
    IFieldMasker masker,
    IValidator<ConfigRoute> routeValidator,
    IValidator<CreateDraftRequest> createValidator,
    IValidator<UpdatePayloadRequest> updateValidator)
{
    private static string Resource(ConfigType type) => $"config:{type}";

    public async Task<ConfigVersionResponse> GetVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var found = await FindAsync(versionId, cancellationToken);
        if (found is null)
        {
            // Someone with no config rights at all gets 403, so ids cannot be probed.
            await guard.RequireAnyAsync("config:", PermissionAction.View, cancellationToken);
            throw new ConfigNotFoundException("Config version", versionId);
        }

        var grant = await guard.RequireAsync(Resource(found.Value.Definition.Type), PermissionAction.View, null, cancellationToken);
        return masker.Mask(ConfigVersionResponse.From(found.Value.Definition, found.Value.Version), grant);
    }

    public async Task<ConfigVersionResponse> GetLatestAsync(string type, string code, CancellationToken cancellationToken)
    {
        var route = Validate(type, code);
        var grant = await guard.RequireAsync(Resource(route.ConfigType), PermissionAction.View, null, cancellationToken);

        var definition = await FindDefinitionAsync(route, cancellationToken)
                         ?? throw new ConfigNotFoundException("Config definition", Guid.Empty);
        var version = await db.Versions
            .Where(v => v.DefinitionId == definition.Id
                        && (v.Status == ConfigVersionStatus.Published || v.Status == ConfigVersionStatus.Locked))
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConfigNotFoundException("Published version of " + definition.Code, definition.Id);

        return masker.Mask(ConfigVersionResponse.From(definition, version), grant);
    }

    public async Task<ConfigVersionResponse> CreateDraftAsync(string type, string code, CreateDraftRequest request, CancellationToken cancellationToken)
    {
        var route = Validate(type, code);
        Validate(createValidator, request);
        var grant = await guard.RequireAsync(Resource(route.ConfigType), PermissionAction.Edit, null, cancellationToken);

        var definition = await FindDefinitionAsync(route, cancellationToken);
        var payload = request.Payload?.GetRawText();
        if (definition is null)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || payload is null)
            {
                throw Invalid("name", "A new config needs a name and a payload.");
            }

            var definitionId = await admin.CreateDefinitionAsync(route.ConfigType, route.Code, request.Name, cancellationToken);
            return await DraftAsync(definitionId, payload, grant, cancellationToken);
        }

        return await DraftAsync(definition.Id, payload, grant, cancellationToken);
    }

    /// <summary>A change to a published config is always a new version: copy the newest one into a draft.</summary>
    public async Task<ConfigVersionResponse> NewVersionAsync(string type, string code, CancellationToken cancellationToken)
    {
        var route = Validate(type, code);
        var grant = await guard.RequireAsync(Resource(route.ConfigType), PermissionAction.Edit, null, cancellationToken);
        var definition = await FindDefinitionAsync(route, cancellationToken)
                         ?? throw new ConfigNotFoundException("Config definition", Guid.Empty);
        return await DraftAsync(definition.Id, null, grant, cancellationToken);
    }

    public async Task<ConfigVersionResponse> UpdateDraftAsync(Guid versionId, UpdatePayloadRequest request, CancellationToken cancellationToken)
    {
        var (grant, _) = await RequireOnVersionAsync(versionId, PermissionAction.Edit, cancellationToken);
        Validate(updateValidator, request);
        await admin.UpdateDraftAsync(versionId, request.Payload!.Value.GetRawText(), cancellationToken);
        return await ReadAsync(versionId, grant, cancellationToken);
    }

    public async Task<ConfigVersionResponse> PublishAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var (grant, _) = await RequireOnVersionAsync(versionId, PermissionAction.Approve, cancellationToken);
        await admin.PublishAsync(versionId, cancellationToken);
        return await ReadAsync(versionId, grant, cancellationToken);
    }

    public async Task<ConfigVersionResponse> LockAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var (grant, _) = await RequireOnVersionAsync(versionId, PermissionAction.Approve, cancellationToken);
        await admin.LockAsync(versionId, cancellationToken);
        return await ReadAsync(versionId, grant, cancellationToken);
    }

    private async Task<ConfigVersionResponse> DraftAsync(Guid definitionId, string? payload, AccessGrant grant, CancellationToken cancellationToken)
    {
        var versionId = await admin.CreateDraftAsync(definitionId, payload, cancellationToken);
        return await ReadAsync(versionId, grant, cancellationToken);
    }

    private async Task<ConfigVersionResponse> ReadAsync(Guid versionId, AccessGrant grant, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var found = await FindAsync(versionId, cancellationToken) ?? throw new ConfigNotFoundException("Config version", versionId);
        return masker.Mask(ConfigVersionResponse.From(found.Definition, found.Version), grant);
    }

    private async Task<(AccessGrant Grant, ConfigType Type)> RequireOnVersionAsync(Guid versionId, PermissionAction action, CancellationToken cancellationToken)
    {
        var found = await FindAsync(versionId, cancellationToken);
        if (found is null)
        {
            await guard.RequireAnyAsync("config:", action, cancellationToken);
            throw new ConfigNotFoundException("Config version", versionId);
        }

        var type = found.Value.Definition.Type;
        return (await guard.RequireAsync(Resource(type), action, null, cancellationToken), type);
    }

    private async Task<(ConfigDefinition Definition, ConfigVersion Version)?> FindAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await db.Versions.SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var definition = await db.Definitions.SingleAsync(d => d.Id == version.DefinitionId, cancellationToken);
        return (definition, version);
    }

    private Task<ConfigDefinition?> FindDefinitionAsync(ConfigRoute route, CancellationToken cancellationToken) =>
        db.Definitions.SingleOrDefaultAsync(d => d.Type == route.ConfigType && d.Code == route.Code, cancellationToken);

    private ConfigRoute Validate(string type, string code)
    {
        var route = new ConfigRoute(type, code);
        Validate(routeValidator, route);
        return route;
    }

    private static void Validate<T>(IValidator<T> validator, T value)
    {
        var result = validator.Validate(value);
        if (!result.IsValid)
        {
            throw new ValidationFailedException(result.Errors
                .GroupBy(e => JsonName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }
    }

    private static string JsonName(string property) =>
        string.IsNullOrEmpty(property) ? property : char.ToLowerInvariant(property[0]) + property[1..];

    private static ValidationFailedException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
