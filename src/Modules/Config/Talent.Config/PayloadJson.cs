using System.Text.Json;
using System.Text.Json.Serialization;

namespace Talent.Config;

/// <summary>Strict JSON reading for config payloads: unknown properties and unknown enum values are errors.</summary>
internal static class PayloadJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static T Read<T>(string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidConfigPayloadException("The payload is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                   ?? throw new InvalidConfigPayloadException("The payload is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidConfigPayloadException($"The payload is not valid: {ex.Message}");
        }
    }

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
