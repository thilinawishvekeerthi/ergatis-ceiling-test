using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;

namespace Talent.Workflow;

/// <summary>The single place a transition is checked and applied, for people and for the SLA tick alike.</summary>
internal sealed class TransitionExecutor(TaskPlanner planner, TimeProvider clock)
{
    public async Task ExecuteAsync(
        WorkflowInstance instance,
        WorkflowDefinitionModel model,
        string action,
        string actor,
        string? comment,
        bool checkActor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(model);

        if (checkActor)
        {
            instance.EnsureCanAct(actor);
        }

        var transition = model.FindTransition(instance.CurrentState, action, instance.Context)
                         ?? throw new ActionNotAllowedException(instance.CurrentState, action);
        var target = model.GetState(transition.To);

        // Resolve first: if nobody can be found, nothing has changed.
        var next = target.IsFinal ? null : await planner.PlanAsync(target, instance.SubjectEmployeeId, cancellationToken);
        instance.Apply(transition, target, actor, comment, clock.GetUtcNow(), next);
    }
}
