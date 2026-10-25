using System.ComponentModel.DataAnnotations;

namespace Talent.Integration.Sync;

internal enum SyncScheduler
{
    /// <summary>No automatic runs. Dev and tests trigger the sync by hand.</summary>
    Disabled,

    /// <summary>Hangfire recurring job on PostgreSQL storage.</summary>
    Hangfire,
}

/// <summary>Bound from <c>Integration:Sync</c>.</summary>
internal sealed class SyncOptions : IValidatableObject
{
    public const string Section = "Integration:Sync";

    public SyncScheduler Scheduler { get; set; } = SyncScheduler.Disabled;

    /// <summary>Cron in UTC. 02:00 leaves four hours before the 06:00 deadline.</summary>
    public string Cron { get; set; } = "0 2 * * *";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Scheduler == SyncScheduler.Hangfire && string.IsNullOrWhiteSpace(Cron))
        {
            yield return new ValidationResult("Cron is required when the scheduler is Hangfire.", [nameof(Cron)]);
        }
    }
}

/// <summary>Bound from <c>Integration:CORE_HR</c>. Only Mock exists so far; DbView and Rest come with the real adapters.</summary>
internal sealed class SourceSystemOptions
{
    public string Mode { get; set; } = "Mock";

    public MockOptions Mock { get; set; } = new();

    internal sealed class MockOptions
    {
        public int Seed { get; set; } = 1;

        public int Employees { get; set; } = 200;
    }
}
