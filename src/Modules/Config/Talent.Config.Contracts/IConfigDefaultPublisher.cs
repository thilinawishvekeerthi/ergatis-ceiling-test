namespace Talent.Config.Contracts;

/// <summary>Lets a module ship a default version of its own config type, for example the permission matrix.</summary>
public interface IConfigDefaultPublisher
{
    /// <summary>Creates and publishes version 1 when the definition has no version yet. Does nothing otherwise, so a client's later versions are never overwritten.</summary>
    Task EnsurePublishedAsync(ConfigType type, string code, string name, string payloadJson, CancellationToken cancellationToken);
}
