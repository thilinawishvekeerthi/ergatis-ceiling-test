using Talent.Config.Contracts;
using Talent.Shared.Errors;

namespace Talent.Assessment.NineBox;

/// <summary>The grid version an evaluation uses, with the rubric of each axis.</summary>
internal sealed record NineBoxConfig(
    Guid GridVersionId,
    NineBoxScheme Scheme,
    IReadOnlyList<RubricLevel> PerformanceLevels,
    IReadOnlyList<RubricLevel> PotentialLevels);

internal sealed class NineBoxNotConfiguredException()
    : TalentProblemException(409, "The nine-box grid is not configured yet. Ask your HR administrator to publish it.");

/// <summary>
/// Reads the nine-box through the Config module's contract. There are no review cycles in this slice,
/// so a new evaluation uses the newest published grid and stores that version id on the record.
/// </summary>
internal sealed class NineBoxConfigProvider(IConfigVersionReader reader)
{
    public async Task<NineBoxConfig> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var grid = await reader.GetLatestPublishedAsync(ConfigType.Grid, NineBoxDefaults.GridCode, cancellationToken)
                   ?? throw new NineBoxNotConfiguredException();
        var scheme = NineBoxScheme.Parse(grid.PayloadJson);

        return new NineBoxConfig(
            grid.Id,
            scheme,
            await RubricAsync(scheme.PerformanceScaleVersionId, cancellationToken),
            await RubricAsync(scheme.PotentialScaleVersionId, cancellationToken));
    }

    private async Task<IReadOnlyList<RubricLevel>> RubricAsync(Guid? scaleVersionId, CancellationToken cancellationToken)
    {
        if (scaleVersionId is not { } id || await reader.GetAsync(id, cancellationToken) is not { } scale)
        {
            // A grid without a bound scale still works; the reviewer just sees level numbers.
            return [.. Enumerable.Range(NineBoxRules.MinLevel, NineBoxRules.MaxLevel).Select(l => new RubricLevel(l, l.ToString(System.Globalization.CultureInfo.InvariantCulture), null))];
        }

        return RubricScale.Parse(scale.PayloadJson);
    }
}
