using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Talent.Config.Contracts;
using Talent.Shared.Auditing;
using Talent.Shared.Security;
using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;
using Talent.Workflow.Persistence;
using Talent.Workflow.Versions;

namespace Talent.Workflow;

/// <summary>Admin operations on workflow definitions: create, edit a draft, publish (ADR-02). Every change is audited.</summary>
internal sealed class WorkflowAdminService(WorkflowDbContext db, ICurrentUser user, TimeProvider clock)
{
    public async Task<Guid> CreateDefinitionAsync(string code, string name, CancellationToken cancellationToken)
    {
        var definition = WorkflowDefinition.Create(code, name);
        if (await db.Definitions.AnyAsync(d => d.Code == code, cancellationToken))
        {
            throw new WorkflowConflictException($"A workflow named {code} already exists.");
        }

        db.Definitions.Add(definition);
        db.AddAudit(user, clock, "CreateDefinition", nameof(WorkflowDefinition), definition.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { definition.Code }));
        await SaveAsync(cancellationToken);
        return definition.Id;
    }

    /// <summary>Opens a new draft. With no payload it copies the newest version (a change is always a new version).</summary>
    public async Task<Guid> CreateDraftAsync(Guid definitionId, string? payloadJson, CancellationToken cancellationToken)
    {
        var definition = await db.Definitions.SingleOrDefaultAsync(d => d.Id == definitionId, cancellationToken)
                         ?? throw new WorkflowNotFoundException("Workflow definition", definitionId.ToString());
        var versions = await db.Versions.Where(v => v.DefinitionId == definitionId)
            .OrderByDescending(v => v.VersionNo).ToListAsync(cancellationToken);
        if (versions.Any(v => v.Status == ConfigVersionStatus.Draft))
        {
            throw new WorkflowConflictException($"{definition.Code} already has an open draft. Edit it or publish it first.");
        }

        WorkflowVersion draft;
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            draft = (versions.FirstOrDefault() ?? throw new InvalidWorkflowDefinitionException("The first version needs a payload."))
                .CreateNextVersion();
        }
        else
        {
            draft = WorkflowVersion.CreateDraft(definitionId, (versions.FirstOrDefault()?.VersionNo ?? 0) + 1, payloadJson);
        }

        db.Versions.Add(draft);
        db.AddAudit(user, clock, "CreateDraft", nameof(WorkflowVersion), draft.Id.ToString());
        await SaveAsync(cancellationToken);
        return draft.Id;
    }

    public async Task UpdateDraftAsync(Guid versionId, string payloadJson, CancellationToken cancellationToken)
    {
        var version = await FindVersionAsync(versionId, cancellationToken);
        version.UpdatePayload(payloadJson);
        db.AddAudit(user, clock, "UpdateDraft", nameof(WorkflowVersion), versionId.ToString());
        await SaveAsync(cancellationToken);
    }

    public async Task PublishAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await FindVersionAsync(versionId, cancellationToken);
        var definition = await db.Definitions.SingleAsync(d => d.Id == version.DefinitionId, cancellationToken);

        version.Publish(definition.Code, user.UserId, clock.GetUtcNow());
        db.AddAudit(user, clock, "Publish", nameof(WorkflowVersion), versionId.ToString(),
            beforeJson: """{"status":"Draft"}""",
            afterJson: JsonSerializer.Serialize(new { status = "Published", version.VersionNo, version.Checksum }));
        await SaveAsync(cancellationToken);
    }

    private async Task<WorkflowVersion> FindVersionAsync(Guid versionId, CancellationToken cancellationToken) =>
        await db.Versions.SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken)
        ?? throw new WorkflowNotFoundException("Workflow version", versionId.ToString());

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WorkflowConflictException("Someone else changed this workflow. Reload and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new WorkflowConflictException("That workflow already exists.");
        }
    }
}
