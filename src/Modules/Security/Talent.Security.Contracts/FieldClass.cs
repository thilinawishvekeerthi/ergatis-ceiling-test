namespace Talent.Security.Contracts;

/// <summary>
/// Sensitivity of a field. <see cref="TalentConfidential"/> (nine-box, potential, successor status, risk of loss)
/// is masked unless a grant is explicit, and always for the employee's own record.
/// </summary>
public enum FieldClass
{
    Public = 0,
    Internal = 1,
    TalentConfidential = 2,
}

/// <summary>Marks the class of a DTO property. A property without it is treated as <see cref="FieldClass.TalentConfidential"/> (deny by default).</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FieldClassificationAttribute(FieldClass fieldClass) : Attribute
{
    public FieldClass FieldClass { get; } = fieldClass;
}
