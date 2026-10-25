using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talent.Config.Contracts;
using Talent.Config.Grids;
using Talent.Config.Persistence;
using Talent.Config.Scales;
using Talent.Config.Versions;
using Talent.Shared.Auditing;
using Talent.Shared.Security;

namespace Talent.Config;

/// <summary>Admin operations on versioned config: create, edit a draft, publish (ADR-02). Every change is audited.</summary>
internal sealed class ConfigAdminService(ConfigDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<Guid> CreateDefinitionAsync(ConfigType type, string code, string name, CancellationToken cancellationToken)
    {
        var definition = ConfigDefinition.Create(type, code, name);

        if (await db.Definitions.AnyAsync(d => d.Type == type && d.Code == code, cancellationToken))
        {
            throw new ConfigConflictException($"A {type} named {code} already exists.");
        }

        db.Definitions.Add(definition);
        db.AddAudit(user, clock, "CreateDefinition", nameof(ConfigDefinition), definition.Id.ToString(),
            afterJson: PayloadJson.Write(new { definition.Type, definition.Code }));
        await SaveAsync(cancellationToken);
        return definition.Id;
    }

    /// <summary>
    /// Opens a new draft. With no payload it copies the newest version of the definition (a change is always a new version).
    /// </summary>
    public async Task<Guid> CreateDraftAsync(Guid definitionId, string? payloadJson, CancellationToken cancellationToken)
    {
        var definition = await db.Definitions.SingleOrDefaultAsync(d => d.Id == definitionId, cancellationToken)
                         ?? throw new ConfigNotFoundException("Config definition", definitionId);

        var versions = await db.Versions
            .Where(v => v.DefinitionId == definitionId)
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync(cancellationToken);

        if (versions.Any(v => v.Status == ConfigVersionStatus.Draft))
        {
            throw new ConfigConflictException($"{definition.Code} already has an open draft. Edit it or publish it first.");
        }

        ConfigVersion draft;
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            var latest = versions.FirstOrDefault()
                         ?? throw new InvalidConfigPayloadException("The first version needs a payload.");
            draft = latest.CreateNextVersion();
        }
        else
        {
            draft = ConfigVersion.CreateDraft(definitionId, (versions.FirstOrDefault()?.VersionNo ?? 0) + 1, payloadJson);
        }

        db.Versions.Add(draft);
        db.AddAudit(user, clock, "CreateDraft", nameof(ConfigVersion), draft.Id.ToString());
        await SaveAsync(cancellationToken);
        return draft.Id;
    }

    public async Task UpdateDraftAsync(Guid versionId, string payloadJson, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidConfigPayloadException("The payload is empty.");
        }

        var version = await FindVersionAsync(versionId, cancellationToken);
        version.UpdatePayload(payloadJson);
        db.AddAudit(user, clock, "UpdateDraft", nameof(ConfigVersion), versionId.ToString());
        await SaveAsync(cancellationToken);
    }

    public async Task PublishAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await FindVersionAsync(versionId, cancellationToken);
        var definition = await db.Definitions.SingleAsync(d => d.Id == version.DefinitionId, cancellationToken);

        if (version.Status != ConfigVersionStatus.Draft)
        {
            throw new InvalidConfigTransitionException("publish", version.Status.ToString());
        }

        await ValidateAsync(definition.Type, version.PayloadJson, cancellationToken);

        version.Publish(user.UserId, clock.GetUtcNow());

        if (definition.Type == ConfigType.Scale)
        {
            foreach (var level in ScaleDefinition.Parse(version.PayloadJson).Levels)
            {
                db.ScaleLevels.Add(new ScaleLevelRow
                {
                    VersionId = version.Id,
                    Level = level.Level,
                    Label = level.Label,
                    MinScore = level.MinScore,
                    Band = level.Band,
                });
            }
        }

        version.Raise(new ConfigVersionPublished(
            clock.GetUtcNow(), version.Id, definition.Id, definition.Type, definition.Code, version.VersionNo));
        db.AddAudit(user, clock, "Publish", nameof(ConfigVersion), version.Id.ToString(),
            beforeJson: """{"status":"Draft"}""",
            afterJson: PayloadJson.Write(new { status = "Published", version.VersionNo, version.Checksum }));
        await SaveAsync(cancellationToken);
    }

    private async Task ValidateAsync(ConfigType type, string payloadJson, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case ConfigType.Scale:
                _ = ScaleDefinition.Parse(payloadJson);
                break;
            case ConfigType.Grid:
                await ValidateGridAsync(GridPayload.Parse(payloadJson), cancellationToken);
                break;
            default:
                // Other types get their own validators when their modules arrive. For now the payload must be a JSON object.
                _ = PayloadJson.Read<System.Text.Json.Nodes.JsonObject>(payloadJson);
                break;
        }
    }

    private async Task ValidateGridAsync(GridPayload payload, CancellationToken cancellationToken)
    {
        var bound = new[] { payload.ColumnScaleVersionId, payload.RowScaleVersionId }.OfType<Guid>().Distinct().ToList();
        if (bound.Count == 0)
        {
            return;
        }

        var usable = await (
            from v in db.Versions
            join d in db.Definitions on v.DefinitionId equals d.Id
            where bound.Contains(v.Id)
                  && d.Type == ConfigType.Scale
                  && (v.Status == ConfigVersionStatus.Published || v.Status == ConfigVersionStatus.Locked)
            select v.Id).CountAsync(cancellationToken);

        if (usable != bound.Count)
        {
            throw new InvalidConfigPayloadException("Each axis scale must be a published scale version.");
        }
    }

    private async Task<ConfigVersion> FindVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        await db.Versions.SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken)
        ?? throw new ConfigNotFoundException("Config version", versionId);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConfigConflictException("Someone else changed this config. Reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConfigConflictException("That config already exists.");
        }
    }
}
