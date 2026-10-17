using Kanimambo.SharedKernel.Web;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace Kanimambo.ArchitectureTests;

public sealed class ControllerRulesTests
{
    private static Types AllTypes => Types.InAssemblies(KanimamboAssemblies.All);

    [Fact]
    public void Controllers_never_touch_persistence()
    {
        AllTypes.That().Inherit(typeof(ControllerBase))
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql")
            .GetResult()
            .ShouldPass("Controllers are thin: they dispatch through IMediator and never use a DbContext");
    }

    [Fact]
    public void Controllers_derive_from_the_shared_base_so_errors_are_uniform()
    {
        AllTypes.That().Inherit(typeof(ControllerBase)).And().AreNotAbstract()
            .Should().Inherit(typeof(ApiControllerBase))
            .GetResult()
            .ShouldPass("Every controller maps Results to ProblemDetails the same way");
    }

    [Fact]
    public void Controllers_live_in_module_api_projects()
    {
        AllTypes.That().Inherit(typeof(ControllerBase)).And().AreNotAbstract()
            .Should().ResideInNamespaceMatching(@"^Kanimambo\.\w+\.Api(\.|$)")
            .GetResult()
            .ShouldPass("Controllers belong to Kanimambo.<Module>.Api");
    }
}
