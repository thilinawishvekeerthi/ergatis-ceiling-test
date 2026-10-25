using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

/// <summary>
/// Bound from <c>Integration:CORE_HR</c>. Only Mock exists so far; DbView and Rest come with the real adapters.
/// There is no default mode: Mock must be chosen on purpose and is refused outside Development.
/// </summary>
internal sealed class SourceSystemOptions
{
    public const string MockMode = "Mock";

    public string? Mode { get; set; }

    public MockOptions Mock { get; set; } = new();

    internal sealed class MockOptions
    {
        public int Seed { get; set; } = 1;

        public int Employees { get; set; } = 200;
    }
}

/// <summary>Stops fabricated Mock data from being synced into a real environment.</summary>
internal sealed class SourceSystemOptionsValidator(IServiceProvider services) : IValidateOptions<SourceSystemOptions>
{
    public ValidateOptionsResult Validate(string? name, SourceSystemOptions options)
    {
        var environment = services.GetService<IHostEnvironment>();
        if (string.Equals(options.Mode, SourceSystemOptions.MockMode, StringComparison.OrdinalIgnoreCase)
            && environment is not null && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail(
                $"Integration:CORE_HR:Mode 'Mock' generates fake employees and is only allowed in Development, not {environment.EnvironmentName}.");
        }

        return ValidateOptionsResult.Success;
    }
}
