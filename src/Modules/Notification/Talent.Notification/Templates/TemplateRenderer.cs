using System.Text.RegularExpressions;

namespace Talent.Notification.Templates;

/// <summary>Fills <c>{Name}</c> placeholders. Values are inserted as plain text and are never read as placeholders themselves.</summary>
internal static partial class TemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        return PlaceholderPattern().Replace(template, m => values.GetValueOrDefault(m.Groups[1].Value) ?? "");
    }

    public static IReadOnlyList<string> PlaceholdersIn(string template) =>
        [.. PlaceholderPattern().Matches(template).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal)];

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)\}")]
    private static partial Regex PlaceholderPattern();
}
