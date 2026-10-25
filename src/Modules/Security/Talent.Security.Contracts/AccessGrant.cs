namespace Talent.Security.Contracts;

/// <summary>What the permission matrix allows one caller to do with one object and action.</summary>
/// <param name="Scopes">Data scopes of all matching grants, combined.</param>
/// <param name="MaxFieldClass">Highest non-confidential class the caller may read.</param>
/// <param name="TalentConfidential">True only with an explicit grant.</param>
public sealed record AccessGrant(IReadOnlySet<DataScope> Scopes, FieldClass MaxFieldClass, bool TalentConfidential)
{
    public bool CoversAllEmployees => Scopes.Contains(DataScope.All);

    public bool CanRead(FieldClass fieldClass, bool isOwnRecord = false) => fieldClass switch
    {
        FieldClass.Public => true,
        FieldClass.Internal => MaxFieldClass >= FieldClass.Internal,
        _ => TalentConfidential && !isOwnRecord,
    };
}
