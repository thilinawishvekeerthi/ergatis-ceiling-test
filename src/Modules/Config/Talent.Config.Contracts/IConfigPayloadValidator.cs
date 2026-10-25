namespace Talent.Config.Contracts;

/// <summary>
/// Plug point: a module that owns a config type checks its payload when a version is published.
/// Config knows only the generic rules (shape, grids, scales); the owner knows what its payload means.
/// </summary>
public interface IConfigPayloadValidator
{
    ConfigType Type { get; }

    /// <summary>Returns messages that are safe to show to the admin. Empty means valid.</summary>
    IReadOnlyList<string> Validate(string payloadJson);
}
