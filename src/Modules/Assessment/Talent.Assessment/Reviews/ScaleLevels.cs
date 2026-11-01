using System.Text.Json;
using Talent.Assessment.Cycles;

namespace Talent.Assessment.Reviews;

/// <summary>The levels of a pinned rating scale and the score each one counts as on a grid axis.</summary>
internal sealed class ScaleLevels
{
    private readonly Dictionary<int, (string Label, decimal MinScore)> _levels;

    private ScaleLevels(Dictionary<int, (string, decimal)> levels) => _levels = levels;

    public IReadOnlyCollection<int> Levels => _levels.Keys;

    public static ScaleLevels Parse(string payloadJson, string axis)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var levels = new Dictionary<int, (string, decimal)>();
            foreach (var item in doc.RootElement.GetProperty("levels").EnumerateArray())
            {
                var level = item.GetProperty("level").GetInt32();
                var label = item.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                levels[level] = (label, item.GetProperty("minScore").GetDecimal());
            }

            return levels.Count == 0
                ? throw new InvalidReviewException($"The {axis} scale has no levels.")
                : new ScaleLevels(levels);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidReviewException($"The {axis} scale of this cycle cannot be read.");
        }
    }

    /// <summary>The grid-axis score of a level. Throws when the level is not on this scale.</summary>
    public decimal ScoreOf(int level, string axis) =>
        _levels.TryGetValue(level, out var found)
            ? found.MinScore
            : throw new InvalidReviewException($"{axis} level {level} is not on this cycle's scale ({string.Join(", ", _levels.Keys.Order())}).");

    public string LabelOf(int level) => _levels.TryGetValue(level, out var found) ? found.Label : "";
}
