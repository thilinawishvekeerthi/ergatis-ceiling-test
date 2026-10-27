using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Talent.Security.Contracts;
using Talent.Shared.Errors;
using Talent.Workflow.Contracts;
using Talent.Workflow.Instances;
using Talent.Workflow.Persistence;

namespace Talent.Workflow.Api;

/// <summary>Start a workflow about the caller. The subject employee is always the caller, never a value from the browser.</summary>
internal sealed record StartWorkflowBody(string? DefinitionCode, string? SubjectType, string? SubjectId, Dictionary<string, string>? Context);

internal sealed record ActionBody(string? Action, string? Comment);

/// <summary>One inbox row: the task plus enough of its workflow to decide without opening it.</summary>
internal sealed record InboxItem(
    ApprovalTaskDto Task,
    string DefinitionCode,
    string SubjectType,
    string SubjectId,
    string StartedBy,
    IReadOnlyDictionary<string, string> Context);

/// <summary>
/// The HTTP rules on top of <see cref="IWorkflowService"/>. A workflow is visible only to the people in it:
/// the starter, anyone who was ever assigned a task, and anyone who acted on it. Everyone else gets 404.
/// </summary>
internal sealed partial class WorkflowApi(IWorkflowService workflows, WorkflowDbContext db, ISecurityUser user)
{
    private const int MaxContextEntries = 20;
    private const int MaxContextValue = 500;
    private const int MaxComment = 2000;

    public async Task<WorkflowInstanceDto> StartAsync(StartWorkflowBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var employeeId = user.EmployeeId ?? throw new ForbiddenException("Your login is not linked to an employee.");

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(body.DefinitionCode) || !CodePattern().IsMatch(body.DefinitionCode))
        {
            errors["definitionCode"] = ["A workflow code of capital letters, digits and underscores is required."];
        }

        if (string.IsNullOrWhiteSpace(body.SubjectType) || !NamePattern().IsMatch(body.SubjectType))
        {
            errors["subjectType"] = ["A subject type of 2 to 50 letters, digits and underscores is required."];
        }

        if (body.SubjectId is { Length: > 100 })
        {
            errors["subjectId"] = ["The subject id can be at most 100 characters."];
        }

        var context = body.Context ?? [];
        if (context.Count > MaxContextEntries
            || context.Any(c => !NamePattern().IsMatch(c.Key) || c.Value is null || c.Value.Length > MaxContextValue))
        {
            errors["context"] = [$"At most {MaxContextEntries} entries: names of letters, digits and underscores, values up to {MaxContextValue} characters."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        var id = await workflows.StartAsync(
            new StartWorkflowRequest(
                body.DefinitionCode!, body.SubjectType!, string.IsNullOrWhiteSpace(body.SubjectId) ? Guid.NewGuid().ToString() : body.SubjectId,
                employeeId.ToString(), context),
            cancellationToken);
        return await workflows.GetAsync(id, cancellationToken);
    }

    public async Task<WorkflowInstanceDto> GetAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        await RequireParticipantAsync(instanceId, cancellationToken);
        return await workflows.GetAsync(instanceId, cancellationToken);
    }

    public async Task<WorkflowInstanceDto> ActAsync(Guid instanceId, ActionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        await RequireParticipantAsync(instanceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(body.Action) || !NamePattern().IsMatch(body.Action))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["action"] = ["An action such as Submit or Approve is required."] });
        }

        if (body.Comment is { Length: > MaxComment })
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["comment"] = [$"A comment can be at most {MaxComment} characters."] });
        }

        await workflows.TransitionAsync(instanceId, body.Action, string.IsNullOrWhiteSpace(body.Comment) ? null : body.Comment.Trim(), cancellationToken);
        return await workflows.GetAsync(instanceId, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowInstanceDto>> ListMineAsync(CancellationToken cancellationToken) =>
        workflows.ListStartedByMeAsync(cancellationToken);

    public async Task<IReadOnlyList<InboxItem>> GetInboxAsync(CancellationToken cancellationToken)
    {
        var tasks = await workflows.GetInboxAsync(cancellationToken);
        var ids = tasks.Select(t => t.InstanceId).Distinct().ToList();
        var instances = await db.Instances.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken);
        return [.. tasks.Select(t =>
        {
            var i = instances[t.InstanceId];
            return new InboxItem(t, i.DefinitionCode, i.SubjectType, i.SubjectId, i.StartedBy, i.Context);
        })];
    }

    private async Task RequireParticipantAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        var me = user.UserId;
        var visible = await db.Instances.AnyAsync(
            i => i.Id == instanceId
                 && (i.StartedBy == me
                     || db.Tasks.Any(t => t.InstanceId == i.Id && db.Assignees.Any(a => a.TaskId == t.Id && a.UserId == me))
                     || db.History.Any(h => h.InstanceId == i.Id && h.Actor == me)),
            cancellationToken);
        if (!visible)
        {
            throw new WorkflowNotFoundException("Workflow", instanceId.ToString());
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{1,49}$")]
    private static partial Regex NamePattern();
}
