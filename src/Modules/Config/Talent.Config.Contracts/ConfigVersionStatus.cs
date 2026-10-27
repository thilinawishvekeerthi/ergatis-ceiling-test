namespace Talent.Config.Contracts;

/// <summary>Lifecycle of a configuration version (ADR-02).</summary>
public enum ConfigVersionStatus
{
    Draft = 0,
    Published = 1,
    Locked = 2,
    Retired = 3,
}
