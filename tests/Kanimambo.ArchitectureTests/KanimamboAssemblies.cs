using System.Reflection;
using NetArchTest.Rules;

namespace Kanimambo.ArchitectureTests;

public sealed record ModuleAssembly(string Module, string Layer, Assembly Assembly);

/// <summary>
/// Discovers every Kanimambo assembly next to the tests, so module rules apply automatically as
/// modules are added (Kanimambo.&lt;Module&gt;.&lt;Layer&gt;) without editing these tests.
/// </summary>
internal static class KanimamboAssemblies
{
    public static readonly string[] ModuleLayers = ["Domain", "Application", "Infrastructure", "Api", "Contracts"];

    /// <summary>Layers of another module that must never be referenced (only Contracts are public).</summary>
    public static readonly string[] InternalLayers = ["Domain", "Application", "Infrastructure", "Api"];

    public static readonly IReadOnlyList<Assembly> All = Directory
        .GetFiles(AppContext.BaseDirectory, "Kanimambo.*.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => !name!.EndsWith("Tests", StringComparison.Ordinal))
        .Select(name => Assembly.Load(name!))
        .ToArray();

    public static readonly IReadOnlyList<ModuleAssembly> Modules = All
        .Select(assembly => (Assembly: assembly, Parts: assembly.GetName().Name!.Split('.')))
        .Where(x => x.Parts.Length == 3 && x.Parts[1] != "SharedKernel" && ModuleLayers.Contains(x.Parts[2]))
        .Select(x => new ModuleAssembly(x.Parts[1], x.Parts[2], x.Assembly))
        .ToArray();

    public static IReadOnlyList<string> ModuleNames => Modules.Select(m => m.Module).Distinct().ToArray();

    public static Assembly Named(string name) =>
        All.SingleOrDefault(a => a.GetName().Name == name)
        ?? throw new InvalidOperationException($"Assembly '{name}' was not found next to the architecture tests.");

    public static void ShouldPass(this NetArchTest.Rules.TestResult result, string rule)
    {
        var failing = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue($"{rule}. Violations: {string.Join(", ", failing)}");
    }
}
