using System.Reflection;
using NetArchTest.Rules;

namespace Kanimambo.ArchitectureTests;

/// <summary>
/// Clean Architecture inside each module, and isolation between modules. These rules run over
/// whatever modules exist; with no module yet they pass trivially and start biting in Phase 1.
/// </summary>
public sealed class ModuleRulesTests
{
    private static readonly string[] InfrastructureFrameworks =
        ["Microsoft.EntityFrameworkCore", "Npgsql", "StackExchange.Redis"];

    [Fact]
    public void Domain_depends_on_nothing_but_the_shared_kernel()
    {
        foreach (var domain in Layer("Domain"))
        {
            var forbidden = InfrastructureFrameworks
                .Append("Microsoft.AspNetCore")
                .Concat(AllModuleNamespaces("Application", "Infrastructure", "Api"));

            Types.InAssembly(domain.Assembly)
                .ShouldNot().HaveDependencyOnAny([.. forbidden])
                .GetResult()
                .ShouldPass($"{domain.Module}.Domain must not depend on outer layers or frameworks");
        }
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_http()
    {
        foreach (var application in Layer("Application"))
        {
            var forbidden = InfrastructureFrameworks
                .Append("Microsoft.AspNetCore")
                .Concat(AllModuleNamespaces("Infrastructure", "Api"));

            Types.InAssembly(application.Assembly)
                .ShouldNot().HaveDependencyOnAny([.. forbidden])
                .GetResult()
                .ShouldPass($"{application.Module}.Application must only see ports, not adapters");
        }
    }

    [Fact]
    public void Api_layer_reaches_the_module_only_through_the_application_layer()
    {
        foreach (var api in Layer("Api"))
        {
            Types.InAssembly(api.Assembly)
                .ShouldNot().HaveDependencyOnAny([.. InfrastructureFrameworks, .. AllModuleNamespaces("Infrastructure", "Domain")])
                .GetResult()
                .ShouldPass($"{api.Module}.Api controllers only send requests through IMediator");
        }
    }

    [Fact]
    public void Every_module_assembly_follows_the_naming_convention_so_rules_cover_it()
    {
        var unexpected = KanimamboAssemblies.All
            .Select(a => a.GetName().Name!)
            .Where(n => n.Split('.') is [_, var module, _] && module is not "SharedKernel" && !KanimamboAssemblies.Modules.Any(m => m.Assembly.GetName().Name == n))
            .ToArray();

        unexpected.ShouldBeEmpty("Module assemblies must be named Kanimambo.<Module>.<Domain|Application|Infrastructure|Api|Contracts>");
        KanimamboAssemblies.ModuleNames.ShouldContain("Catalog");
    }

    [Fact]
    public void Modules_only_talk_to_each_other_through_contracts()
    {
        foreach (var assembly in KanimamboAssemblies.Modules)
        {
            var otherModulesInternals = KanimamboAssemblies.ModuleNames
                .Where(module => module != assembly.Module)
                .SelectMany(module => KanimamboAssemblies.InternalLayers.Select(layer => $"Kanimambo.{module}.{layer}"))
                .ToArray();

            if (otherModulesInternals.Length == 0)
            {
                continue;
            }

            Types.InAssembly(assembly.Assembly)
                .ShouldNot().HaveDependencyOnAny(otherModulesInternals)
                .GetResult()
                .ShouldPass($"{assembly.Module}.{assembly.Layer} may only reference other modules' Contracts");
        }
    }

    [Fact]
    public void Domain_entities_expose_no_public_setters()
    {
        var offenders = Layer("Domain")
            .SelectMany(domain => domain.Assembly.GetTypes())
            .Where(type => type.IsClass && IsEntity(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToArray();

        offenders.ShouldBeEmpty("Entities protect their invariants: state changes go through behavior methods");
    }

    private static IEnumerable<ModuleAssembly> Layer(string layer) =>
        KanimamboAssemblies.Modules.Where(m => m.Layer == layer);

    private static IEnumerable<string> AllModuleNamespaces(params string[] layers) =>
        KanimamboAssemblies.ModuleNames.SelectMany(module => layers.Select(layer => $"Kanimambo.{module}.{layer}"));

    private static bool IsEntity(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(SharedKernel.Domain.Entity<>))
            {
                return true;
            }
        }

        return false;
    }
}
