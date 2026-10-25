using System.Text.RegularExpressions;
using Talent.Workflow.Definitions;

namespace Talent.Workflow.Versions;

/// <summary>A named workflow, for example IDP_APPROVAL. Its versions hold the states and transitions (ADR-02).</summary>
internal sealed partial class WorkflowDefinition
{
    private WorkflowDefinition()
    {
        Code = "";
        Name = "";
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }

    public static WorkflowDefinition Create(string code, string name)
    {
        if (string.IsNullOrEmpty(code) || !CodePattern().IsMatch(code))
        {
            throw new InvalidWorkflowDefinitionException("The code must be 2 to 50 characters: capital letters, digits and underscores, starting with a letter.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new InvalidWorkflowDefinitionException("The name must be 1 to 200 characters.");
        }

        return new WorkflowDefinition { Id = Guid.NewGuid(), Code = code, Name = name.Trim() };
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex CodePattern();
}
