using System.Runtime.CompilerServices;
using Talent.Integration.Contracts;

namespace Talent.Integration.Adapters;

internal abstract class MockAdapter<T>(Func<IReadOnlyList<SourceRecord<T>>> source) : ISourceAdapter<T>
    where T : class
{
    public string System => SyncSystems.CoreHr;

    public async IAsyncEnumerable<SourceRecord<T>> FetchChangesAsync(
        DateTimeOffset since, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var record in source().Where(r => r.ModifiedAt > since))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }

        await Task.CompletedTask;
    }

    public Task<SourceRecord<T>?> FetchOneAsync(string externalId, CancellationToken cancellationToken) =>
        Task.FromResult(source().FirstOrDefault(r => r.ExternalId == externalId));
}

internal sealed class MockOrgUnitAdapter(MockSourceData data) : MockAdapter<OrgUnitSource>(() => data.OrgUnits);

internal sealed class MockPositionAdapter(MockSourceData data) : MockAdapter<PositionSource>(() => data.Positions);

internal sealed class MockEmployeeAdapter(MockSourceData data) : MockAdapter<EmployeeSource>(() => data.Employees);

internal static class SyncSystems
{
    public const string CoreHr = "CORE_HR";
}
