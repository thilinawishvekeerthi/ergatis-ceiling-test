namespace Talent.Config.Scales;

/// <summary>One level of a rating scale. <see cref="Band"/> is the stable reporting band that survives new versions.</summary>
internal sealed record ScaleLevel(int Level, string Label, decimal MinScore, string Band);

/// <summary>A rating scale of 2 to 10 levels (ADR-03), read from a config payload.</summary>
internal sealed class ScaleDefinition
{
    public const int MinLevels = 2;
    public const int MaxLevels = 10;

    private ScaleDefinition(IReadOnlyList<ScaleLevel> levels) => Levels = levels;

    public IReadOnlyList<ScaleLevel> Levels { get; }

    public static ScaleDefinition Parse(string json)
    {
        var dto = PayloadJson.Read<ScalePayload>(json);
        var levels = dto.Levels ?? throw new InvalidConfigPayloadException("The scale needs a 'levels' list.");

        if (levels.Count is < MinLevels or > MaxLevels)
        {
            throw new InvalidConfigPayloadException($"A scale needs {MinLevels} to {MaxLevels} levels but has {levels.Count}.");
        }

        var result = new List<ScaleLevel>(levels.Count);
        for (var i = 0; i < levels.Count; i++)
        {
            var item = levels[i];
            if (item.Level != i + 1)
            {
                throw new InvalidConfigPayloadException($"Level numbers must run 1 to {levels.Count} in order.");
            }

            if (string.IsNullOrWhiteSpace(item.Label))
            {
                throw new InvalidConfigPayloadException($"Level {item.Level} needs a label.");
            }

            if (string.IsNullOrWhiteSpace(item.Band))
            {
                throw new InvalidConfigPayloadException($"Level {item.Level} needs a reporting band.");
            }

            if (i > 0 && item.MinScore <= levels[i - 1].MinScore)
            {
                throw new InvalidConfigPayloadException("Minimum scores must be strictly ascending.");
            }

            result.Add(new ScaleLevel(item.Level, item.Label.Trim(), item.MinScore, item.Band.Trim()));
        }

        return new ScaleDefinition(result);
    }

    private sealed class ScalePayload
    {
        public List<LevelPayload>? Levels { get; set; }
    }

    private sealed class LevelPayload
    {
        public int Level { get; set; }
        public string? Label { get; set; }
        public decimal MinScore { get; set; }
        public string? Band { get; set; }
    }
}
