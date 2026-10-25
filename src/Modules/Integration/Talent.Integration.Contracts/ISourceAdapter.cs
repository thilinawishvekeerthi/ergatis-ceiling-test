namespace Talent.Integration.Contracts;

/// <summary>One record read from a source system, still in the source's own shape and keys.</summary>
public sealed record SourceRecord<T>(string ExternalId, T Payload, DateTimeOffset ModifiedAt)
    where T : class;

/// <summary>
/// Plug point: one implementation per source system and entity, chosen by
/// <c>Integration:&lt;System&gt;:Mode = Mock | DbView | Rest</c> (ADR-08). Read-only.
/// </summary>
public interface ISourceAdapter<T>
    where T : class
{
    /// <summary>Source system code such as CORE_HR or LMS.</summary>
    string System { get; }

    IAsyncEnumerable<SourceRecord<T>> FetchChangesAsync(DateTimeOffset since, CancellationToken cancellationToken);

    Task<SourceRecord<T>?> FetchOneAsync(string externalId, CancellationToken cancellationToken);
}
