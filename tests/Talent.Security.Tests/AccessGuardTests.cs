using Microsoft.Extensions.Logging.Abstractions;
using Talent.Config.Contracts;
using Talent.Security.Access;
using Talent.Security.Contracts;
using Talent.Security.Matrix;
using Talent.Security.Scopes;
using Talent.Shared.Errors;

namespace Talent.Security.Tests;

public sealed class AccessGuardTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid MyReport = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private const string Matrix = """
        {"grants":[
          {"role":"HR_ADMIN","resource":"config:*","actions":["View","Edit","Approve"],"scope":"All","fieldClass":"Internal"},
          {"role":"MANAGER","resource":"appraisal:Result","actions":["View","Approve","Calibrate"],"scope":"DirectReports","fieldClass":"Internal"},
          {"role":"EMPLOYEE","resource":"appraisal:Result","actions":["View","Edit"],"scope":"Self","fieldClass":"Internal"},
          {"role":"ANYONE","resource":"appraisal:Result","actions":["Edit"],"scope":"All","fieldClass":"Public"}]}
        """;

    private sealed class FakeVersions(string? payload) : IConfigVersionReader
    {
        public Task<ConfigVersionDto?> GetAsync(Guid versionId, CancellationToken cancellationToken) => Task.FromResult<ConfigVersionDto?>(null);

        public Task<ConfigVersionDto?> GetLatestPublishedAsync(ConfigType type, string code, CancellationToken cancellationToken) =>
            Task.FromResult(payload is null
                ? null
                : new ConfigVersionDto(Guid.NewGuid(), Guid.NewGuid(), type, code, 1, ConfigVersionStatus.Published, payload, null, null));
    }

    private sealed class Reports : IScopeMemberSource
    {
        public Task<IReadOnlyCollection<Guid>> GetEmployeeIdsAsync(DataScope scope, Guid caller, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(scope == DataScope.DirectReports ? [MyReport] : []);
    }

    private static AccessGuard GuardFor(SecurityContext user, string? matrix = Matrix) =>
        new(user, new PermissionMatrixProvider(new FakeVersions(matrix), NullLogger<PermissionMatrixProvider>.Instance),
            new DataScopeService(user, new Reports()));

    private static SecurityContext Signed(Guid? employee, params string[] roles) => SecurityContext.ForTest("u1", employee, roles);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Require_WhenNotSignedIn_ThrowsNotAuthenticated() =>
        await Should.ThrowAsync<NotAuthenticatedException>(
            () => GuardFor(new SecurityContext()).RequireAsync("config:Scale", PermissionAction.View, null, Ct));

    [Fact]
    public async Task Require_WhenNoEmployee_ThrowsForbidden() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(null, "HR_ADMIN")).RequireAsync("config:Scale", PermissionAction.View, null, Ct));

    [Fact]
    public async Task Require_WhenRoleLacksAction_ThrowsForbidden() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(Me, "EMPLOYEE")).RequireAsync("config:Scale", PermissionAction.View, null, Ct));

    [Fact]
    public async Task Require_WhenNoMatrixIsPublished_DeniesEverything() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(Me, "HR_ADMIN"), matrix: null).RequireAsync("config:Scale", PermissionAction.View, null, Ct));

    [Fact]
    public async Task Require_WhenPublishedMatrixIsBroken_DeniesEverything() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(Me, "HR_ADMIN"), matrix: """{"grants":"nope"}""").RequireAsync("config:Scale", PermissionAction.View, null, Ct));

    [Fact]
    public async Task Require_ForOrganisationWideObjectWithoutAllScope_ThrowsForbidden() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(Me, "EMPLOYEE")).RequireAsync("appraisal:Result", PermissionAction.View, null, Ct));

    [Fact]
    public async Task Require_ForRecordInScope_ReturnsGrant()
    {
        var grant = await GuardFor(Signed(Me, "MANAGER")).RequireAsync("appraisal:Result", PermissionAction.View, MyReport, Ct);

        grant.MaxFieldClass.ShouldBe(FieldClass.Internal);
    }

    [Fact]
    public async Task Require_ForRecordOutsideScope_ThrowsForbidden() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(Me, "MANAGER")).RequireAsync("appraisal:Result", PermissionAction.View, Stranger, Ct));

    [Fact]
    public async Task Require_WhenManagerApprovesOwnRecord_ThrowsForbiddenEvenIfInScope()
    {
        // MANAGER reaches only reports, so own record is out of scope. Add SELF scope to prove the COI check itself.
        var user = Signed(Me, "MANAGER", "EMPLOYEE");

        var ex = await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(user).RequireAsync("appraisal:Result", PermissionAction.Approve, Me, Ct));

        ex.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task Require_WhenEmployeeEditsOwnRecord_ThrowsForbiddenBecauseOfConflictOfInterest() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(Me, "EMPLOYEE")).RequireAsync("appraisal:Result", PermissionAction.Edit, Me, Ct));

    [Fact]
    public async Task Require_WhenEmployeeViewsOwnRecord_Passes() =>
        (await GuardFor(Signed(Me, "EMPLOYEE")).RequireAsync("appraisal:Result", PermissionAction.View, Me, Ct)).ShouldNotBeNull();

    [Fact]
    public async Task Require_WhenAllScopeGrantEditsOwnRecord_StillThrowsForbidden() =>
        await Should.ThrowAsync<ForbiddenException>(
            () => GuardFor(Signed(Me, "ANYONE")).RequireAsync("appraisal:Result", PermissionAction.Edit, Me, Ct));

    [Fact]
    public async Task RequireAny_WhenSomeGrantUnderPrefix_Passes() =>
        await Should.NotThrowAsync(() => GuardFor(Signed(Me, "HR_ADMIN")).RequireAnyAsync("config:", PermissionAction.Edit, Ct));

    [Fact]
    public async Task RequireAny_WhenNoGrantUnderPrefix_ThrowsForbidden() =>
        await Should.ThrowAsync<ForbiddenException>(() => GuardFor(Signed(Me, "HR_ADMIN")).RequireAnyAsync("appraisal:", PermissionAction.Edit, Ct));

    [Fact]
    public void AccessGrant_CanRead_FollowsFieldClassRules()
    {
        var internalOnly = new AccessGrant(new HashSet<DataScope> { DataScope.All }, FieldClass.Internal, false);
        var confidential = internalOnly with { TalentConfidential = true };

        internalOnly.CanRead(FieldClass.Public).ShouldBeTrue();
        internalOnly.CanRead(FieldClass.Internal).ShouldBeTrue();
        internalOnly.CanRead(FieldClass.TalentConfidential).ShouldBeFalse();
        confidential.CanRead(FieldClass.TalentConfidential).ShouldBeTrue();
        confidential.CanRead(FieldClass.TalentConfidential, isOwnRecord: true).ShouldBeFalse();
    }
}
