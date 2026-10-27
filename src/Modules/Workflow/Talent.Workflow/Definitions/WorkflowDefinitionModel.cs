using System.Text.Json;
using System.Text.Json.Serialization;

namespace Talent.Workflow.Definitions;

/// <summary>
/// The parsed, validated content of one workflow version: states, transitions, guards, approvers and SLAs (ADR-02, ADR-05).
/// Pure: no database, no clock. Everything the state machine decides is decided here and in the instance.
/// </summary>
internal sealed record WorkflowDefinitionModel(
    string InitialState,
    IReadOnlyList<StateDefinition> States,
    IReadOnlyList<TransitionDefinition> Transitions)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static WorkflowDefinitionModel Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidWorkflowDefinitionException("The workflow definition is empty.");
        }

        WorkflowDefinitionModel model;
        try
        {
            model = JsonSerializer.Deserialize<WorkflowDefinitionModel>(json, Options)
                    ?? throw new InvalidWorkflowDefinitionException("The workflow definition is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidWorkflowDefinitionException($"The workflow definition is not valid: {ex.Message}");
        }

        model.Validate();
        return model;
    }

    public string ToCanonicalJson() => JsonSerializer.Serialize(this, Options);

    public StateDefinition GetState(string code) =>
        States.SingleOrDefault(s => s.Code == code)
        ?? throw new InvalidWorkflowDefinitionException($"State {code} is not in the workflow definition.");

    /// <summary>The first transition for this state and action whose guards all pass, or null if the action is not allowed.</summary>
    public TransitionDefinition? FindTransition(string fromState, string action, IReadOnlyDictionary<string, string> context) =>
        Transitions.FirstOrDefault(t => t.From == fromState && t.Action == action && t.GuardsPass(context));

    public IReadOnlyList<string> AvailableActions(string state, IReadOnlyDictionary<string, string> context) =>
        [.. Transitions.Where(t => t.From == state && t.GuardsPass(context)).Select(t => t.Action).Distinct()];

    private void Validate()
    {
        if (States is null || States.Count == 0 || Transitions is null)
        {
            throw new InvalidWorkflowDefinitionException("A workflow needs states and transitions.");
        }

        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in States)
        {
            if (string.IsNullOrWhiteSpace(state?.Code) || state.Code.Length > 50)
            {
                throw new InvalidWorkflowDefinitionException("Every state needs a code of 1 to 50 characters.");
            }

            if (!codes.Add(state.Code))
            {
                throw new InvalidWorkflowDefinitionException($"State {state.Code} appears twice.");
            }

            if (state.IsFinal && state.Approval is not null)
            {
                throw new InvalidWorkflowDefinitionException($"Final state {state.Code} cannot wait for an approval.");
            }
        }

        if (!codes.Contains(InitialState ?? ""))
        {
            throw new InvalidWorkflowDefinitionException($"The initial state {InitialState} is not a state of this workflow.");
        }

        if (States.Single(s => s.Code == InitialState).IsFinal)
        {
            throw new InvalidWorkflowDefinitionException("The initial state cannot be final.");
        }

        if (!States.Any(s => s.IsFinal))
        {
            throw new InvalidWorkflowDefinitionException("A workflow needs at least one final state.");
        }

        ValidateTransitions(codes);
        ValidateApprovals();
        ValidateReachability();
    }

    private void ValidateTransitions(HashSet<string> codes)
    {
        foreach (var t in Transitions)
        {
            if (string.IsNullOrWhiteSpace(t?.Action) || t.Action.Length > 50)
            {
                throw new InvalidWorkflowDefinitionException("Every transition needs an action of 1 to 50 characters.");
            }

            if (!codes.Contains(t.From) || !codes.Contains(t.To))
            {
                throw new InvalidWorkflowDefinitionException($"Transition {t.Action} uses a state that does not exist.");
            }

            if (States.Single(s => s.Code == t.From).IsFinal)
            {
                throw new InvalidWorkflowDefinitionException($"Final state {t.From} cannot have outgoing transitions.");
            }

            foreach (var guard in t.Guards ?? [])
            {
                if (string.IsNullOrWhiteSpace(guard?.Field) || guard.Value is null || !Enum.IsDefined(guard.Op))
                {
                    throw new InvalidWorkflowDefinitionException($"Transition {t.Action} has an incomplete guard.");
                }
            }
        }
    }

    private void ValidateApprovals()
    {
        foreach (var state in States.Where(s => s.Approval is not null))
        {
            ValidateActor(state.Approval!.Approver, state.Code);
            if (!Transitions.Any(t => t.From == state.Code))
            {
                throw new InvalidWorkflowDefinitionException($"Approval state {state.Code} has no way out.");
            }

            if (state.Approval.Sla is not { } sla)
            {
                continue;
            }

            if (sla.Hours is < 1 or > 24 * 365 || sla.MaxEscalations is < 0 or > 10)
            {
                throw new InvalidWorkflowDefinitionException($"The SLA of state {state.Code} is out of range.");
            }

            ValidateActor(sla.EscalateTo, state.Code);
            if (sla.ExpiryAction is not null && !Transitions.Any(t => t.From == state.Code && t.Action == sla.ExpiryAction))
            {
                throw new InvalidWorkflowDefinitionException(
                    $"The expiry action {sla.ExpiryAction} of state {state.Code} is not a transition of that state.");
            }
        }
    }

    private static void ValidateActor(ActorRef? actor, string state)
    {
        if (actor is null || !Enum.IsDefined(actor.Kind)
            || (actor.Kind != ActorKind.Manager && string.IsNullOrWhiteSpace(actor.Value))
            || (actor.Value?.Length ?? 0) > 200)
        {
            throw new InvalidWorkflowDefinitionException($"State {state} has an incomplete actor. Role and User need a value.");
        }
    }

    private void ValidateReachability()
    {
        var seen = new HashSet<string> { InitialState };
        var queue = new Queue<string>([InitialState]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var t in Transitions.Where(t => t.From == current && seen.Add(t.To)))
            {
                queue.Enqueue(t.To);
            }
        }

        var unreachable = States.Select(s => s.Code).Except(seen).ToList();
        if (unreachable.Count > 0)
        {
            throw new InvalidWorkflowDefinitionException($"State {unreachable[0]} cannot be reached from the initial state.");
        }

        // Walk the transitions backwards from the final states: any state left over is a dead end where an instance would stay forever.
        var canFinish = new HashSet<string>(States.Where(s => s.IsFinal).Select(s => s.Code));
        var backQueue = new Queue<string>(canFinish);
        while (backQueue.TryDequeue(out var current))
        {
            foreach (var t in Transitions.Where(t => t.To == current && canFinish.Add(t.From)))
            {
                backQueue.Enqueue(t.From);
            }
        }

        var stuck = States.Select(s => s.Code).Except(canFinish).ToList();
        if (stuck.Count > 0)
        {
            throw new InvalidWorkflowDefinitionException($"State {stuck[0]} has no path to a final state.");
        }
    }
}
