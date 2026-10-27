using Talent.Config.Contracts;

namespace Talent.Security.Matrix;

/// <summary>Runs when a permission matrix is published, so a broken or lock-out matrix never becomes the active one.</summary>
internal sealed class PermissionMatrixPayloadValidator : IConfigPayloadValidator
{
    public ConfigType Type => ConfigType.PermissionMatrix;

    public IReadOnlyList<string> Validate(string payloadJson)
    {
        return PermissionMatrix.TryParse(payloadJson, out _, out var errors) ? [] : errors;
    }
}
