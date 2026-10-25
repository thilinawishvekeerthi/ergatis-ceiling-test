using Talent.Security.Contracts;
using Talent.Security.Masking;

namespace Talent.Security.Tests;

public sealed class FieldMaskerTests
{
    private sealed record Sample(
        [property: FieldClassification(FieldClass.Public)] Guid Id,
        [property: FieldClassification(FieldClass.Public)] string Name,
        [property: FieldClassification(FieldClass.Internal)] string? Grade,
        [property: FieldClassification(FieldClass.TalentConfidential)] string? NineBoxCell,
        [property: FieldClassification(FieldClass.TalentConfidential)] int? Potential,
        string? Unclassified);

    private static readonly Sample Full = new(Guid.NewGuid(), "Amal", "M2", "Star", 3, "secret");

    private static AccessGrant Grant(FieldClass max, bool confidential) =>
        new(new HashSet<DataScope> { DataScope.All }, max, confidential);

    private readonly FieldMasker _masker = new();

    [Fact]
    public void Mask_WhenPublicOnly_HidesInternalAndConfidential()
    {
        var masked = _masker.Mask(Full, Grant(FieldClass.Public, false));

        masked.Name.ShouldBe("Amal");
        masked.Grade.ShouldBeNull();
        masked.NineBoxCell.ShouldBeNull();
        masked.Potential.ShouldBeNull();
    }

    [Fact]
    public void Mask_WhenInternalWithoutConfidentialGrant_HidesOnlyConfidential()
    {
        var masked = _masker.Mask(Full, Grant(FieldClass.Internal, false));

        masked.Grade.ShouldBe("M2");
        masked.NineBoxCell.ShouldBeNull();
        masked.Potential.ShouldBeNull();
    }

    [Fact]
    public void Mask_WhenExplicitConfidentialGrant_ShowsConfidential()
    {
        var masked = _masker.Mask(Full, Grant(FieldClass.Internal, true));

        masked.NineBoxCell.ShouldBe("Star");
        masked.Potential.ShouldBe(3);
    }

    [Fact]
    public void Mask_WhenOwnRecord_HidesConfidentialEvenWithGrant()
    {
        var masked = _masker.Mask(Full, Grant(FieldClass.Internal, true), isOwnRecord: true);

        masked.Grade.ShouldBe("M2");
        masked.NineBoxCell.ShouldBeNull();
        masked.Potential.ShouldBeNull();
    }

    [Fact]
    public void Mask_WhenPropertyUnclassified_TreatsItAsConfidential()
    {
        _masker.Mask(Full, Grant(FieldClass.Internal, false)).Unclassified.ShouldBeNull();
        _masker.Mask(Full, Grant(FieldClass.Internal, true)).Unclassified.ShouldBe("secret");
    }

    [Fact]
    public void Mask_KeepsPublicValueTypes()
    {
        _masker.Mask(Full, Grant(FieldClass.Public, false)).Id.ShouldBe(Full.Id);
    }

    [Fact]
    public void Mask_DoesNotChangeTheOriginal()
    {
        _ = _masker.Mask(Full, Grant(FieldClass.Public, false));

        Full.NineBoxCell.ShouldBe("Star");
    }

    private sealed record NotNullableSecret(
        [property: FieldClassification(FieldClass.Public)] string Name,
        [property: FieldClassification(FieldClass.Internal)] int Salary);

    [Fact]
    public void Mask_WhenMaskedPropertyCannotBeNull_Throws()
    {
        Should.Throw<InvalidOperationException>(
            () => _masker.Mask(new NotNullableSecret("A", 5), Grant(FieldClass.Public, false)));
    }
}
