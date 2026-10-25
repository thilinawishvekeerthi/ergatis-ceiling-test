using System.Reflection;
using NetArchTest.Rules;
using Talent.Config.Versions;
using Talent.Integration;
using Talent.Security;

namespace Talent.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly[] Modules = [typeof(ConfigVersion).Assembly, typeof(SecurityModule).Assembly, typeof(IntegrationModule).Assembly];

    [Fact]
    public void Modules_ReferenceOnlyContractsOfOtherModules()
    {
        foreach (var module in Modules)
        {
            var own = module.GetName().Name;
            var forbidden = module.GetReferencedAssemblies()
                .Select(a => a.Name!)
                .Where(n => n.StartsWith("Talent.", StringComparison.Ordinal)
                            && !n.EndsWith(".Contracts", StringComparison.Ordinal)
                            && n != "Talent.Shared"
                            && n != own);

            forbidden.ShouldBeEmpty($"{own} breaks a module boundary");
        }
    }

    [Fact]
    public void ConfigDomain_DoesNotDependOnEntityFramework()
    {
        var result = Types.InAssembly(typeof(ConfigVersion).Assembly)
            .That().ResideInNamespaceStartingWith("Talent.Config.Versions")
            .Or().ResideInNamespaceStartingWith("Talent.Config.Grids")
            .Or().ResideInNamespaceStartingWith("Talent.Config.Scales")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void ConfigImplementation_IsInternal()
    {
        var result = Types.InAssembly(typeof(ConfigVersion).Assembly)
            .That().AreClasses()
            .And().DoNotHaveNameEndingWith("Module")
            .And().DoNotResideInNamespaceContaining("Migrations")
            .Should().NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void SecurityImplementation_IsInternal()
    {
        var result = Types.InAssembly(typeof(SecurityModule).Assembly)
            .That().AreClasses()
            .And().DoNotHaveNameEndingWith("Module")
            .And().DoNotResideInNamespaceContaining("Migrations")
            .And().DoNotHaveName("Program")
            .And().AreNotNested()
            .Should().NotBePublic()
            .GetResult();

        (result.FailingTypeNames ?? []).ShouldBeEmpty();
    }

    [Fact]
    public void ConfigApi_DoesNotReachIntoSecurityImplementation()
    {
        var result = Types.InAssembly(typeof(ConfigVersion).Assembly)
            .ShouldNot().HaveDependencyOn("Talent.Security.Access")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void IntegrationImplementation_IsInternal()
    {
        var result = Types.InAssembly(typeof(IntegrationModule).Assembly)
            .That().AreClasses()
            .And().DoNotHaveNameEndingWith("Module")
            .And().DoNotResideInNamespaceContaining("Migrations")
            .And().DoNotHaveName("Program")
            .Should().NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void IntegrationContracts_DoNotDependOnEntityFramework()
    {
        var result = Types.InAssembly(typeof(Talent.Integration.Contracts.IEmployeeReader).Assembly)
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}
