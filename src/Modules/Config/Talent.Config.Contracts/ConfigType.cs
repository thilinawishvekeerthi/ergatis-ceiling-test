namespace Talent.Config.Contracts;

/// <summary>What a versioned config object configures (ADR-02).</summary>
public enum ConfigType
{
    Scale = 0,
    Grid = 1,
    EligibilityRule = 2,
    ScoringWeights = 3,
    Workflow = 4,
    PermissionMatrix = 5,
}
