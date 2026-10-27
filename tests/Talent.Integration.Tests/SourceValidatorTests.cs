using Talent.Integration.Adapters;
using Talent.Integration.Contracts;

namespace Talent.Integration.Tests;

public sealed class SourceValidatorTests
{
    private static EmployeeSource Good => new("E1", "Ada", "Lovelace", "ada@example.com", null, null, null, new DateOnly(2020, 1, 1), "Active");

    [Fact]
    public void Validate_WhenEmployeeIsGood_ReturnsNull() => SourceValidator.Validate(Good).ShouldBeNull();

    [Fact]
    public void Validate_WhenEmployeeNumberBlank_ReturnsError() =>
        SourceValidator.Validate(Good with { EmployeeNumber = " " }).ShouldNotBeNull();

    [Fact]
    public void Validate_WhenNameMissing_ReturnsError() =>
        SourceValidator.Validate(Good with { LastName = "" }).ShouldNotBeNull();

    [Fact]
    public void Validate_WhenEmailMalformed_ReturnsError() =>
        SourceValidator.Validate(Good with { Email = "not-an-email" }).ShouldNotBeNull();

    [Fact]
    public void Validate_WhenEmailAbsent_IsAllowed() =>
        SourceValidator.Validate(Good with { Email = null }).ShouldBeNull();

    [Fact]
    public void Validate_WhenStatusUnknown_ReturnsError() =>
        SourceValidator.Validate(Good with { Status = "Zombie" }).ShouldNotBeNull();

    [Fact]
    public void Validate_WhenFieldTooLong_ReturnsError() =>
        SourceValidator.Validate(Good with { FirstName = new string('x', 201) }).ShouldNotBeNull();

    [Fact]
    public void Validate_WhenOrgUnitIsItsOwnParent_ReturnsError() =>
        SourceValidator.Validate(new OrgUnitSource("A", "Unit", "A", true)).ShouldNotBeNull();

    [Fact]
    public void Validate_WhenPositionTitleBlank_ReturnsError() =>
        SourceValidator.Validate(new PositionSource("P1", "", null, true)).ShouldNotBeNull();

    [Fact]
    public void Validate_WhenEmployeeReportsToSelf_ReturnsError() =>
        SourceValidator.Validate(Good with { ManagerEmployeeNumber = "E1" }).ShouldNotBeNull();
}
