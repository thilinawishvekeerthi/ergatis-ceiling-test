using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Talent.Shared.Auditing;
using Talent.Shared.Security;
using Talent.Workflow.Contracts;
using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;
using Talent.Workflow.Persistence;

namespace Talent.Workflow;

/// <summary>
/// Handles one overdue approval task: escalate to the SLA's escalation actor, or when escalations run out
/// apply the expiry action, or mark the task breached. Each step is a normal transition with the system as actor.
/// </summary>
internal sealed class SlaProcessor(WorkflowDbContext db, TaskPlanner planner, TransitionExecutor executor, TimeProvider clock)
{
    private static readonly SystemCurrentUser System = new();

    /// <summary>Returns true when the task was due and something was done.</summary>
    public async Task<bool> ProcessAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var instanceId = await db.Tasks.Where(t => t.Id == taskId).Select(t => (Guid?)t.InstanceId).FirstOrDefaultAsync(cancellationToken);
        if (instanceId is null)
        {
            return false;
        }

        var instance = await db.Instances
            .Include(i => i.History)
            .Include(i => i.Tasks).ThenInclude(t => t.Assignees)
            .AsSplitQuery()
            .SingleAsync(i => i.Id == instanceId, cancellationToken);

        var now = clock.GetUtcNow();
        var task = instance.OpenTask;
        if (task is null || task.Id != taskId || task.SlaBreachedAt is not null || task.DueAt is null || task.DueAt > now)
        {
            return false;
        }

        var payload = await db.Versions.AsNoTracking().Where(v => v.Id == instance.DefinitionVersionId)
            .Select(v => v.PayloadJson).SingleAsync(cancellationToken);
        var model = WorkflowDefinitionModel.Parse(payload);
        var sla = model.GetState(task.State).Approval?.Sla;
        if (sla is null)
        {
            return false;
        }

        string action;
        if (task.EscalationLevel < sla.MaxEscalations && await TryEscalateAsync(instance, sla, now, cancellationToken))
        {
            action = "SlaEscalate";
        }
        else if (sla.ExpiryAction is not null)
        {
            await executor.ExecuteAsync(
                instance, model, sla.ExpiryAction, WorkflowInstance.SystemActor, "SLA expired", checkActor: false, cancellationToken);
            action = "SlaExpire";
        }
        else
        {
            instance.MarkSlaBreached(now);
            action = "SlaBreach";
        }

        db.AddAudit(System, clock, action, nameof(WorkflowInstance), instance.Id.ToString(),
            afterJson: JsonSerializer.Serialize(new { state = instance.CurrentState, status = instance.Status.ToString() }));
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> TryEscalateAsync(WorkflowInstance instance, SlaDefinition sla, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IReadOnlyList<Assignment> people;
        try
        {
            people = await planner.ResolveAsync(sla.EscalateTo, instance.SubjectEmployeeId, instance.CurrentState, cancellationToken);
        }
        catch (ActorResolutionException)
        {
            // Nobody to escalate to: treat the escalations as used up.
            return false;
        }

        instance.Escalate(people, now.AddHours(sla.Hours), now);
        return true;
    }
}
