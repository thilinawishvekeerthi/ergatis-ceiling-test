namespace Talent.Workflow.Contracts;

public enum WorkflowErrorKind
{
    Invalid = 0,
    NotFound = 1,
    Forbidden = 2,
    Conflict = 3,
}

/// <summary>Base of every expected workflow failure. The host maps <see cref="Kind"/> to 400, 404, 403 or 409.</summary>
public abstract class WorkflowException(WorkflowErrorKind kind, string message) : Exception(message)
{
    public WorkflowErrorKind Kind { get; } = kind;
}
