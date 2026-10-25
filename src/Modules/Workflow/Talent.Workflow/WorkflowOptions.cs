using System.ComponentModel.DataAnnotations;

namespace Talent.Workflow;

/// <summary>Settings under <c>Workflow</c> in configuration.</summary>
internal sealed class WorkflowOptions
{
    public const string Section = "Workflow";

    /// <summary>Seconds between SLA checks.</summary>
    [Range(1, 86_400)]
    public int SlaTickSeconds { get; set; } = 60;

    /// <summary>Turn off the background SLA check, for example in tests that run the tick by hand.</summary>
    public bool SlaTickEnabled { get; set; } = true;
}
