using Talent.Workflow.Contracts;

namespace Talent.Workflow.Definitions;

internal sealed class InvalidWorkflowDefinitionException(string message) : WorkflowException(WorkflowErrorKind.Invalid, message);
