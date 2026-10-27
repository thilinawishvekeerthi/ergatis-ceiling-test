namespace Talent.Security.Contracts;

/// <summary>Masks on the server. Hidden data never reaches the browser.</summary>
public interface IFieldMasker
{
    /// <summary>
    /// Returns a copy where every property the grant cannot read is set to its default (null).
    /// Masked properties must be nullable. Properties without <see cref="FieldClassificationAttribute"/> count as TalentConfidential.
    /// </summary>
    T Mask<T>(T value, AccessGrant grant, bool isOwnRecord = false)
        where T : class;
}
