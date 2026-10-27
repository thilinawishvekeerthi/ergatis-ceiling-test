using Talent.Workflow.Contracts;

namespace Talent.Workflow.Instances;

internal sealed class ActorNotAllowedException(string state)
    : WorkflowException(WorkflowErrorKind.Forbidden, $"You are not allowed to act on the workflow in state {state}.");

internal sealed class WorkflowNotActiveException(Guid instanceId)
    : WorkflowException(WorkflowErrorKind.Conflict, $"Workflow {instanceId} is already completed.");

internal sealed class CommentRequiredException(string action)
    : WorkflowException(WorkflowErrorKind.Invalid, $"Action {action} needs a comment.");

internal sealed class ActionNotAllowedException(string state, string action)
    : WorkflowException(WorkflowErrorKind.Invalid, $"Action {action} is not allowed in state {state}.");

internal sealed class WorkflowNotFoundException(string what, string id)
    : WorkflowException(WorkflowErrorKind.NotFound, $"{what} {id} was not found.");

internal sealed class WorkflowConflictException(string message)
    : WorkflowException(WorkflowErrorKind.Conflict, message);

internal sealed class ActorResolutionException(string message)
    : WorkflowException(WorkflowErrorKind.Invalid, message);
