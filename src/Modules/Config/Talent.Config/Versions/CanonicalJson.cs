using System.Text.Json;
using System.Text.Json.Nodes;

namespace Talent.Config.Versions;

/// <summary>
/// Writes JSON in one fixed form: object keys sorted, no whitespace.
/// PostgreSQL jsonb reorders keys and changes spacing, so checksums must not depend on the original text.
/// </summary>
internal static class CanonicalJson
{
    public static string Canonicalize(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidConfigPayloadException($"The payload is not valid JSON: {ex.Message}");
        }

        return Sort(node)?.ToJsonString() ?? "null";
    }

    private static JsonNode? Sort(JsonNode? node) => node switch
    {
        JsonObject obj => SortObject(obj),
        JsonArray array => new JsonArray([.. array.Select(Sort)]),
        null => null,
        _ => node.DeepClone(),
    };

    private static JsonObject SortObject(JsonObject obj)
    {
        var sorted = new JsonObject();
        foreach (var pair in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            sorted[pair.Key] = Sort(pair.Value);
        }

        return sorted;
    }
}
