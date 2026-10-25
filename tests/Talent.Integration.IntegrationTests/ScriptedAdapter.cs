using System.Runtime.CompilerServices;
using Talent.Integration.Contracts;

namespace Talent.Integration.IntegrationTests;

/// <summary>A source system under the test's control: fixed records, optional outage.</summary>
internal sealed class ScriptedAdapter<T> : ISourceAdapter<T>
    where T : class
{
    public List<SourceRecord<T>> Records { get; } = [];

    public bool Down { get; set; }

    public string System => "CORE_HR";

    public async IAsyncEnumerable<SourceRecord<T>> FetchChangesAsync(
        DateTimeOffset since, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Down)
        {
            throw new HttpRequestException("source unavailable");
        }

        foreach (var r in Records.Where(r => r.ModifiedAt > since).ToList())
        {
            yield return r;
        }

        await Task.CompletedTask;
    }

    public Task<SourceRecord<T>?> FetchOneAsync(string externalId, CancellationToken cancellationToken) =>
        Task.FromResult(Records.LastOrDefault(r => r.ExternalId == externalId));
}
