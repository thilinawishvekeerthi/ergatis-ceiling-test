using System.Security.Claims;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Talent.Security.Sessions;
using Talent.Security.Roles;

namespace Talent.Security.Tests;

public sealed class OptionsAndRoleSourceTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "t";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static ValidateOptionsResult Validate(SecurityOptions options, string environment) =>
        new SecurityOptionsValidator(new Env(environment)).Validate(null, options);

    [Fact]
    public void Validate_WhenFakeModeInProduction_Fails()
    {
        var result = Validate(new SecurityOptions { Mode = AuthenticationMode.Fake }, Environments.Production);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Fake");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Validate_WhenFakeModeInNonProduction_Passes(string environment) =>
        Validate(new SecurityOptions { Mode = AuthenticationMode.Fake }, environment).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_WhenOidcWithoutAuthority_Fails() =>
        Validate(new SecurityOptions { Mode = AuthenticationMode.Oidc, Oidc = new() { ClientId = "talent" } }, Environments.Production)
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_WhenOidcWithoutClientId_Fails() =>
        Validate(new SecurityOptions { Mode = AuthenticationMode.Oidc, Oidc = new() { Authority = "https://idp.example" } }, Environments.Production)
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_WhenOidcAuthorityIsNotHttps_Fails() =>
        Validate(new SecurityOptions { Mode = AuthenticationMode.Oidc, Oidc = new() { Authority = "http://idp.example", ClientId = "talent" } }, Environments.Production)
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_WhenOidcComplete_Passes() =>
        Validate(new SecurityOptions { Mode = AuthenticationMode.Oidc, Oidc = new() { Authority = "https://idp.example", ClientId = "talent" } }, Environments.Production)
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_WhenCookieNotSecureInProduction_Fails() =>
        Validate(new SecurityOptions
        {
            Mode = AuthenticationMode.Oidc,
            Oidc = new() { Authority = "https://idp.example", ClientId = "talent" },
            Cookie = new() { RequireSecure = false },
        }, Environments.Production).Failed.ShouldBeTrue();

    [Fact]
    public void Defaults_AreSafe()
    {
        var options = new SecurityOptions();

        options.Cookie.RequireSecure.ShouldBeTrue();
        options.Mode.ShouldBe(AuthenticationMode.Oidc);
        options.RoleSource.ShouldBe(RoleSourceKind.TalentManaged);
    }

    [Fact]
    public async Task IdpGroupsRoleSource_MapsGroupClaimsThroughTheConfiguredMap()
    {
        var source = new IdpGroupsRoleSource(Options.Create(new SecurityOptions
        {
            GroupRoleMap = new() { ["cn=hr"] = "HR_ADMIN", ["cn=mgr"] = "MANAGER" },
        }));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("groups", "cn=hr"), new Claim("groups", "cn=unknown"), new Claim("groups", "cn=mgr")], "test"));

        var roles = await source.GetRolesAsync(principal, TestContext.Current.CancellationToken);

        roles.ShouldBe(["HR_ADMIN", "MANAGER"], ignoreOrder: true);
    }

    [Fact]
    public async Task IdpGroupsRoleSource_WhenNoGroups_ReturnsNoRoles()
    {
        var source = new IdpGroupsRoleSource(Options.Create(new SecurityOptions()));

        var roles = await source.GetRolesAsync(new ClaimsPrincipal(new ClaimsIdentity([], "test")), TestContext.Current.CancellationToken);

        roles.ShouldBeEmpty();
    }
}
