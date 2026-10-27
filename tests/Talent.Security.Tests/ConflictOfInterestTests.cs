using Talent.Security.Access;
using Talent.Security.Contracts;

namespace Talent.Security.Tests;

public sealed class ConflictOfInterestTests
{
    private static readonly Guid Me = Guid.NewGuid();

    [Theory]
    [InlineData(PermissionAction.Edit)]
    [InlineData(PermissionAction.Approve)]
    [InlineData(PermissionAction.Calibrate)]
    public void Ensure_WhenActingOnOwnRecord_Throws(PermissionAction action) =>
        Should.Throw<ForbiddenException>(() => ConflictOfInterestGuard.Ensure(action, Me, Me));

    [Theory]
    [InlineData(PermissionAction.View)]
    [InlineData(PermissionAction.Export)]
    public void Ensure_WhenReadingOwnRecord_Passes(PermissionAction action) =>
        Should.NotThrow(() => ConflictOfInterestGuard.Ensure(action, Me, Me));

    [Fact]
    public void Ensure_WhenActingOnSomeoneElse_Passes() =>
        Should.NotThrow(() => ConflictOfInterestGuard.Ensure(PermissionAction.Approve, Me, Guid.NewGuid()));

    [Fact]
    public void Ensure_WhenNoSubject_Passes() =>
        Should.NotThrow(() => ConflictOfInterestGuard.Ensure(PermissionAction.Approve, Me, null));
}
