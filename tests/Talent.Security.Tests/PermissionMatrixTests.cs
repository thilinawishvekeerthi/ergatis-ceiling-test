using Talent.Security.Contracts;
using Talent.Security.Matrix;

namespace Talent.Security.Tests;

public sealed class PermissionMatrixTests
{
    private static PermissionMatrix Parse(string json)
    {
        PermissionMatrix.TryParse(json, out var matrix, out var errors).ShouldBeTrue(string.Join("; ", errors));
        return matrix!;
    }

    private static HashSet<string> Roles(params string[] roles) => [.. roles];

    private const string Basic = """
        {"grants":[
          {"role":"HR_ADMIN","resource":"config:*","actions":["View","Edit","Approve"],"scope":"All","fieldClass":"Internal","talentConfidential":true},
          {"role":"MANAGER","resource":"config:Scale","actions":["View"],"scope":"DirectReports","fieldClass":"Internal"},
          {"role":"EMPLOYEE","resource":"config:Scale","actions":["View"],"scope":"Self","fieldClass":"Public"}]}
        """;

    [Fact]
    public void Evaluate_WhenRoleHasAction_ReturnsGrant()
    {
        var grant = Parse(Basic).Evaluate(Roles("HR_ADMIN"), "config:Grid", PermissionAction.Edit);

        grant.ShouldNotBeNull();
        grant.CoversAllEmployees.ShouldBeTrue();
        grant.MaxFieldClass.ShouldBe(FieldClass.Internal);
        grant.TalentConfidential.ShouldBeTrue();
    }

    [Fact]
    public void TryParse_WhenAGrantIsNull_ReportsAProblemInsteadOfThrowing()
    {
        const string json = """{"grants":[null,{"role":"HR_ADMIN","resource":"*","actions":["Approve"],"scope":"All","fieldClass":"Internal"}]}""";

        PermissionMatrix.TryParse(json, out _, out var errors).ShouldBeFalse();

        errors.ShouldContain(e => e.Contains("Grant 1 is empty", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_WhenNoRoleMatches_DeniesByDefault() =>
        Parse(Basic).Evaluate(Roles("STRANGER"), "config:Scale", PermissionAction.View).ShouldBeNull();

    [Fact]
    public void Evaluate_WhenNoRoles_DeniesByDefault() =>
        Parse(Basic).Evaluate(Roles(), "config:Scale", PermissionAction.View).ShouldBeNull();

    [Fact]
    public void Evaluate_WhenActionNotListed_ReturnsNull() =>
        Parse(Basic).Evaluate(Roles("MANAGER"), "config:Scale", PermissionAction.Edit).ShouldBeNull();

    [Fact]
    public void Evaluate_WhenResourceDiffers_ReturnsNull() =>
        Parse(Basic).Evaluate(Roles("MANAGER"), "config:Grid", PermissionAction.View).ShouldBeNull();

    [Fact]
    public void Evaluate_WhenResourceIsPrefixOfAnother_DoesNotMatch() =>
        Parse(Basic).Evaluate(Roles("MANAGER"), "config:ScaleExtra", PermissionAction.View).ShouldBeNull();

    [Fact]
    public void Evaluate_WithTwoRoles_CombinesScopesAndKeepsHighestFieldClass()
    {
        var grant = Parse(Basic).Evaluate(Roles("MANAGER", "EMPLOYEE"), "config:Scale", PermissionAction.View);

        grant.ShouldNotBeNull();
        grant.Scopes.ShouldBe([DataScope.DirectReports, DataScope.Self], ignoreOrder: true);
        grant.MaxFieldClass.ShouldBe(FieldClass.Internal);
        grant.TalentConfidential.ShouldBeFalse();
    }

    [Fact]
    public void Evaluate_WhenConfidentialNotGranted_ReturnsNoConfidentialAccess() =>
        Parse(Basic).Evaluate(Roles("MANAGER"), "config:Scale", PermissionAction.View)!.TalentConfidential.ShouldBeFalse();

    [Fact]
    public void Evaluate_WithRoleNameInOtherCase_DoesNotMatch() =>
        Parse(Basic).Evaluate(Roles("hr_admin"), "config:Scale", PermissionAction.View).ShouldBeNull();

    [Theory]
    [InlineData("""{"grants":[]}""", "at least one grant")]
    [InlineData("""{"grants":[{"role":"X1","resource":"config:*","actions":["Delete"],"scope":"All","fieldClass":"Internal"}]}""", "")]
    [InlineData("""{"grants":[{"role":"X1","resource":"config:*","actions":["View"],"scope":"Galaxy","fieldClass":"Internal"}]}""", "")]
    [InlineData("""{"grants":[{"role":"X1","resource":"config:*","actions":["View"],"scope":"All","fieldClass":"TalentConfidential"}]}""", "")]
    [InlineData("""{"grants":[{"role":"bad role","resource":"config:*","actions":["View"],"scope":"All","fieldClass":"Internal"}]}""", "")]
    [InlineData("""{"grants":[{"role":"X1","resource":"nonsense","actions":["View"],"scope":"All","fieldClass":"Internal"}]}""", "")]
    [InlineData("""{"grants":[{"role":"X1","resource":"config:*","actions":[],"scope":"All","fieldClass":"Internal"}]}""", "")]
    [InlineData("""{"grants":[{"role":"X1","resource":"config:*","actions":["View"],"scope":"All","fieldClass":"Internal","surprise":1}]}""", "")]
    [InlineData("not json", "")]
    public void TryParse_WhenInvalid_ReportsErrors(string json, string expectedFragment)
    {
        PermissionMatrix.TryParse(json, out var matrix, out var errors).ShouldBeFalse();

        matrix.ShouldBeNull();
        errors.ShouldNotBeEmpty();
        errors.ShouldAllBe(e => !string.IsNullOrWhiteSpace(e));
        if (expectedFragment.Length > 0)
        {
            errors.ShouldContain(e => e.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void TryParse_WhenNobodyCanApproveMatrixChanges_RejectsToAvoidLockout()
    {
        const string json = """
            {"grants":[{"role":"HR_ADMIN","resource":"config:Scale","actions":["View","Edit","Approve"],"scope":"All","fieldClass":"Internal"}]}
            """;

        PermissionMatrix.TryParse(json, out _, out var errors).ShouldBeFalse();

        errors.ShouldContain(e => e.Contains("lock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultMatrix_IsValidAndGivesHrAdminTheMatrixApproval()
    {
        var matrix = Parse(DefaultPermissionMatrix.Json);

        var grant = matrix.Evaluate(Roles(TalentRoles.HrAdmin), "config:PermissionMatrix", PermissionAction.Approve);

        grant.ShouldNotBeNull();
        grant.CoversAllEmployees.ShouldBeTrue();
    }

    [Fact]
    public void DefaultMatrix_GivesEmployeeNoEditAndNoConfidentialAccess()
    {
        var matrix = Parse(DefaultPermissionMatrix.Json);

        matrix.Evaluate(Roles(TalentRoles.Employee), "config:Scale", PermissionAction.Edit).ShouldBeNull();
        matrix.Evaluate(Roles(TalentRoles.Employee), "config:Scale", PermissionAction.View)!.TalentConfidential.ShouldBeFalse();
    }

    [Fact]
    public void HasAnyGrant_WhenRoleHasActionOnSomeResourceUnderPrefix_ReturnsTrue()
    {
        var matrix = Parse(Basic);

        matrix.HasAny(Roles("MANAGER"), "config:", PermissionAction.View).ShouldBeTrue();
        matrix.HasAny(Roles("MANAGER"), "config:", PermissionAction.Edit).ShouldBeFalse();
        matrix.HasAny(Roles("MANAGER"), "reports:", PermissionAction.View).ShouldBeFalse();
    }
}
