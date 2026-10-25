using Hangfire;
using Talent.Integration.Contracts;

namespace Talent.Integration.Sync;

/// <summary>Hangfire entry point for the nightly delta. A second run never overlaps a running one.</summary>
internal sealed class NightlySyncJob(ISyncService sync)
{
    public const string JobId = "integration-nightly-sync";

    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    [AutomaticRetry(Attempts = 3)]
    public Task RunAsync(CancellationToken cancellationToken) => sync.RunDeltaAsync(cancellationToken);
}
