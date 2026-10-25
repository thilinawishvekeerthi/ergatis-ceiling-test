using System.Reflection;
using NetArchTest.Rules;
using Talent.Config.Versions;
using Talent.Workflow.Definitions;
using Talent.Workflow.Instances;

namespace Talent.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly[] Modules = [typeof(ConfigVersion).Assembly, typeof(WorkflowInstance).Assembly];

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
    public void WorkflowDomain_DoesNotDependOnEntityFramework()
    {
        var result = Types.InAssembly(typeof(WorkflowInstance).Assembly)
            .That().ResideInNamespace("Talent.Workflow.Definitions")
            .Or().ResideInNamespace("Talent.Workflow.Instances")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void WorkflowImplementation_IsInternal()
    {
        var result = Types.InAssembly(typeof(WorkflowInstance).Assembly)
            .That().AreClasses()
            .And().DoNotHaveNameEndingWith("Module")
            .And().DoNotResideInNamespaceContaining("Migrations")
            .Should().NotBePublic()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void WorkflowDefinitionModel_DoesNotDependOnPersistence()
    {
        var result = Types.InAssembly(typeof(WorkflowDefinitionModel).Assembly)
            .That().ResideInNamespace("Talent.Workflow.Definitions")
            .ShouldNot().HaveDependencyOn("Talent.Workflow.Persistence")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
