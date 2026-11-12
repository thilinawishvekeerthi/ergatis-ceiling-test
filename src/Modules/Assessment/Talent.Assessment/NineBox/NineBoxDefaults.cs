using System.Text.Json;

namespace Talent.Assessment.NineBox;

/// <summary>
/// The BRD nine-box (TM-10 label map), shipped as config version 1 of its scales and grid.
/// A client changes labels, colours or rubrics by publishing a new version through the Config API, not by editing code.
/// </summary>
internal static class NineBoxDefaults
{
    public const string PerformanceScaleCode = "TALENT_PERFORMANCE_3";
    public const string PotentialScaleCode = "TALENT_POTENTIAL_3";
    public const string GridCode = "TALENT_NINE_BOX";

    public const string HighBand = "HIGH";
    public const string WatchBand = "WATCH";
    public const string LowBand = "LOW";

    public const string PerformanceScale = """
        {"levels":[
          {"level":1,"label":"Low","minScore":1,"band":"LOW","description":"Misses several goals and needs close support."},
          {"level":2,"label":"Medium","minScore":2,"band":"MEDIUM","description":"Meets most goals reliably with normal guidance."},
          {"level":3,"label":"High","minScore":3,"band":"HIGH","description":"Exceeds goals and raises the bar for others."}]}
        """;

    public const string PotentialScale = """
        {"levels":[
          {"level":1,"label":"Low","minScore":1,"band":"LOW","description":"Best suited to the current role for now."},
          {"level":2,"label":"Medium","minScore":2,"band":"MEDIUM","description":"Can grow into a broader role within 2 to 3 years."},
          {"level":3,"label":"High","minScore":3,"band":"HIGH","description":"Ready for a bigger role within 1 to 2 years."}]}
        """;

    // [potential - 1][performance - 1], from BRD §5.
    private static readonly string[][] Labels =
    [
        ["Risk", "Effective", "Trusted pro"],
        ["Inconsistent", "Core player", "High performer"],
        ["Potential gem", "High potential", "Star"],
    ];

    private static readonly Dictionary<string, string> BandColours = new(StringComparer.Ordinal)
    {
        [HighBand] = "#8fd1a8",
        [WatchBand] = "#f7d98b",
        [LowBand] = "#f2a7a0",
    };

    /// <summary>Level n of a 1 to 3 scale lands in column or row n - 1.</summary>
    internal static readonly decimal[] Bounds = [1m, 2m, 3m];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string GridPayload(Guid performanceScaleVersionId, Guid potentialScaleVersionId)
    {
        var cells = new List<object>();
        for (var potential = 1; potential <= NineBoxScheme.Levels; potential++)
        {
            for (var performance = 1; performance <= NineBoxScheme.Levels; performance++)
            {
                var band = NineBoxRules.BandForScore(NineBoxRules.TalentScore(performance, potential));
                cells.Add(new
                {
                    column = performance - 1,
                    row = potential - 1,
                    label = Labels[potential - 1][performance - 1],
                    colour = BandColours[band],
                    band,
                });
            }
        }

        return JsonSerializer.Serialize(
            new
            {
                mode = "Calculated",
                columnScaleVersionId = performanceScaleVersionId,
                rowScaleVersionId = potentialScaleVersionId,
                columnBounds = Bounds,
                rowBounds = Bounds,
                cells,
            },
            Json);
    }
}
