using NetArchTest.Rules;

namespace Kanimambo.ArchitectureTests;

public sealed class SharedKernelRulesTests
{
    [Fact]
    public void Discovery_finds_the_platform_assemblies()
    {
        var names = KanimamboAssemblies.All.Select(a => a.GetName().Name).ToArray();

        names.ShouldContain("Kanimambo.SharedKernel");
        names.ShouldContain("Kanimambo.SharedKernel.Web");
        names.ShouldContain("Kanimambo.Api");
    }

    [Fact]
    public void Dependency_scanning_sees_real_dependencies()
    {
        // Positive control: proves the rules below would catch a violation instead of passing vacuously.
        Types.InAssembly(KanimamboAssemblies.Named("Kanimambo.Api"))
            .That().HaveName("IdempotencyStore")
            .Should().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult()
            .ShouldPass("The idempotency store uses EF Core");
    }

    [Fact]
    public void Shared_kernel_is_independent_of_web_persistence_and_the_host()
    {
        Types.InAssembly(KanimamboAssemblies.Named("Kanimambo.SharedKernel"))
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "StackExchange.Redis",
                "Kanimambo.SharedKernel.Web",
                "Kanimambo.Api")
            .GetResult()
            .ShouldPass("SharedKernel is referenced by every Domain project and must stay framework-agnostic");
    }

    [Fact]
    public void Shared_web_building_blocks_do_not_depend_on_persistence()
    {
        Types.InAssembly(KanimamboAssemblies.Named("Kanimambo.SharedKernel.Web"))
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Npgsql", "Kanimambo.Api")
            .GetResult()
            .ShouldPass("SharedKernel.Web only maps Results to HTTP");
    }
}
