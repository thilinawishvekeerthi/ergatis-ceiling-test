using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Talent.Config.Contracts;
using Talent.Security.Matrix;

namespace Talent.Security.Access;

/// <summary>
/// Loads the newest published permission matrix. A published version never changes, so the parsed matrix is cached by version id.
/// No published matrix, or one that does not parse, means nobody is allowed (fail closed).
/// </summary>
internal sealed partial class PermissionMatrixProvider(IConfigVersionReader versions, ILogger<PermissionMatrixProvider> logger)
{
    private static readonly ConcurrentDictionary<Guid, PermissionMatrix> Cache = new();

    private PermissionMatrix? _perRequest;

    public async Task<PermissionMatrix> GetAsync(CancellationToken cancellationToken)
    {
        if (_perRequest is not null)
        {
            return _perRequest;
        }

        var latest = await versions.GetLatestPublishedAsync(ConfigType.PermissionMatrix, PermissionMatrix.ConfigCode, cancellationToken);
        if (latest is null)
        {
            LogNoMatrix(logger);
            return _perRequest = PermissionMatrix.Empty;
        }

        if (!Cache.TryGetValue(latest.Id, out var matrix))
        {
            if (!PermissionMatrix.TryParse(latest.PayloadJson, out var parsed, out var errors))
            {
                LogInvalidMatrix(logger, latest.Id, errors.Count);
                return _perRequest = PermissionMatrix.Empty;
            }

            matrix = Cache.GetOrAdd(latest.Id, parsed!);
        }

        return _perRequest = matrix;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "No published permission matrix exists, so every request is denied.")]
    private static partial void LogNoMatrix(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Permission matrix version {VersionId} is invalid ({ErrorCount} errors), so every request is denied.")]
    private static partial void LogInvalidMatrix(ILogger logger, Guid versionId, int errorCount);
}
