namespace Talent.Config;

/// <summary>A config payload broke a rule (shape, limits or values). The message is safe to show to the admin.</summary>
internal sealed class InvalidConfigPayloadException(string message) : InvalidOperationException(message);
