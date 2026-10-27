using System.Globalization;

namespace Talent.Workflow.Definitions;

internal enum ActorKind
{
    Manager = 0,
    Role = 1,
    User = 2,
}

internal enum GuardOperator
{
    Eq = 0,
    Neq = 1,
    Gt = 2,
    Gte = 3,
    Lt = 4,
    Lte = 5,
    In = 6,
}

/// <summary>Who an approval goes to. Manager is the manager of the subject employee. Role and User carry a code or id.</summary>
internal sealed record ActorRef(ActorKind Kind, string? Value = null);

internal sealed record SlaDefinition(int Hours, ActorRef EscalateTo, int MaxEscalations = 1, string? ExpiryAction = null);

internal sealed record ApprovalDefinition(ActorRef Approver, SlaDefinition? Sla = null);

internal sealed record StateDefinition(string Code, bool IsFinal = false, ApprovalDefinition? Approval = null);

/// <summary>One condition on instance data: <c>field operator value</c>. No scripting (ADR-04 grammar rule).</summary>
internal sealed record GuardDefinition(string Field, GuardOperator Op, string Value)
{
    public bool Evaluate(IReadOnlyDictionary<string, string> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.TryGetValue(Field, out var actual))
        {
            return false;
        }

        if (Op == GuardOperator.In)
        {
            return Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase));
        }

        if (decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var left)
            && decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var right))
        {
            return Compare(left.CompareTo(right));
        }

        return Op switch
        {
            GuardOperator.Eq => string.Equals(actual, Value, StringComparison.OrdinalIgnoreCase),
            GuardOperator.Neq => !string.Equals(actual, Value, StringComparison.OrdinalIgnoreCase),
            _ => false, // Ordering text has no meaning, so it never matches.
        };
    }

    private bool Compare(int order) => Op switch
    {
        GuardOperator.Eq => order == 0,
        GuardOperator.Neq => order != 0,
        GuardOperator.Gt => order > 0,
        GuardOperator.Gte => order >= 0,
        GuardOperator.Lt => order < 0,
        GuardOperator.Lte => order <= 0,
        _ => false,
    };
}

internal sealed record TransitionDefinition(
    string From,
    string Action,
    string To,
    IReadOnlyList<GuardDefinition>? Guards = null,
    bool RequiresComment = false)
{
    public bool GuardsPass(IReadOnlyDictionary<string, string> context) =>
        Guards is null || Guards.All(g => g.Evaluate(context));
}
