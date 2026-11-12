using Talent.Shared.Errors;

namespace Talent.Assessment.NineBox;

/// <summary>The result of placing one employee: inputs, then the derived box, label, score and band.</summary>
internal sealed record NineBoxPlacement(int Performance, int Potential, int BoxNumber, string Label, int TalentScore, string Band);

/// <summary>
/// BRD §5 business rules as a pure function: no database, no clock, no I/O.
/// Labels and bands come from the configured grid; the formulas are fixed by the BRD.
/// </summary>
internal static class NineBoxRules
{
    public const int MinLevel = 1;
    public const int MaxLevel = NineBoxScheme.Levels;

    public static NineBoxPlacement Place(int performance, int potential, NineBoxScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        var errors = new Dictionary<string, string[]>();
        if (performance is < MinLevel or > MaxLevel)
        {
            errors["performance"] = [$"Performance must be between {MinLevel} and {MaxLevel}, but was {performance}."];
        }

        if (potential is < MinLevel or > MaxLevel)
        {
            errors["potential"] = [$"Potential must be between {MinLevel} and {MaxLevel}, but was {potential}."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationFailedException(errors);
        }

        var cell = scheme.CellAt(performance, potential);
        return new NineBoxPlacement(performance, potential, cell.BoxNumber, cell.Label, TalentScore(performance, potential), cell.Band);
    }

    /// <summary><c>(potential − 1) × 3 + performance</c>: unique 1 to 9, counted from bottom-left.</summary>
    public static int BoxNumber(int performance, int potential) => (potential - 1) * MaxLevel + performance;

    /// <summary><c>performance × potential</c>: 1 to 9.</summary>
    public static int TalentScore(int performance, int potential) => performance * potential;

    /// <summary>BRD §5 bands: High 6 to 9, Watch 3 to 4, Low 1 to 2. Used to build the default grid.</summary>
    public static string BandForScore(int score) => score switch
    {
        >= 6 => NineBoxDefaults.HighBand,
        >= 3 => NineBoxDefaults.WatchBand,
        _ => NineBoxDefaults.LowBand,
    };
}
