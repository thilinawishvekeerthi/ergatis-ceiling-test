namespace Talent.Config.Contracts;

/// <summary>How a person is placed on a grid (ADR-03).</summary>
public enum PlacementMode
{
    Manual = 0,
    Calculated = 1,
    CalculatedWithOverride = 2,
}
