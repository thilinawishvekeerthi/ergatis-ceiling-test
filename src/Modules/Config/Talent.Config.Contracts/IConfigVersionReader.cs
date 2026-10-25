namespace Talent.Config.Contracts;

/// <summary>Read side of the Config module for other modules. Cycles read by pinned version id, never "latest".</summary>
public interface IConfigVersionReader
{
    Task<ConfigVersionDto?> GetAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>The newest Published or Locked version. Used to choose what a new cycle pins.</summary>
    Task<ConfigVersionDto?> GetLatestPublishedAsync(ConfigType type, string code, CancellationToken cancellationToken);
}

/// <summary>Called when a cycle or record first references a version. After that the version is immutable.</summary>
public interface IConfigVersionLocker
{
    Task LockAsync(Guid versionId, CancellationToken cancellationToken);
}
