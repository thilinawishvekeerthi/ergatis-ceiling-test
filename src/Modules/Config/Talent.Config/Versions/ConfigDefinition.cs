using System.Text.RegularExpressions;
using Talent.Config.Contracts;

namespace Talent.Config.Versions;

/// <summary>A named configurable object, for example the performance scale. Its versions hold the content (ADR-02).</summary>
internal sealed partial class ConfigDefinition
{
    private ConfigDefinition()
    {
        Code = "";
        Name = "";
    }

    public Guid Id { get; private set; }
    public ConfigType Type { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }

    public static ConfigDefinition Create(ConfigType type, string code, string name)
    {
        if (!Enum.IsDefined(type))
        {
            throw new InvalidConfigPayloadException("Unknown config type.");
        }

        if (string.IsNullOrEmpty(code) || !CodePattern().IsMatch(code))
        {
            throw new InvalidConfigPayloadException("The code must be 2 to 50 characters: capital letters, digits and underscores, starting with a letter.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new InvalidConfigPayloadException("The name must be 1 to 200 characters.");
        }

        return new ConfigDefinition { Id = Guid.NewGuid(), Type = type, Code = code, Name = name.Trim() };
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex CodePattern();
}
