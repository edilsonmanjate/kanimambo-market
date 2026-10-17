using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.SharedKernel.Messaging;

/// <summary>Maps request types to the module that owns them (by assembly), to resolve per-module services.</summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<Assembly, string> _moduleByAssembly;

    internal ModuleRegistry(Dictionary<Assembly, string> moduleByAssembly) => _moduleByAssembly = moduleByAssembly;

    public IReadOnlyCollection<string> Modules => _moduleByAssembly.Values.Distinct().ToArray();

    public string GetModuleOf(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return _moduleByAssembly.TryGetValue(requestType.Assembly, out var module)
            ? module
            : throw new InvalidOperationException(
                $"'{requestType.FullName}' is not declared in an assembly registered with MediatorOptions.AddModule.");
    }

    internal TService GetModuleService<TService>(Type requestType, IServiceProvider services)
        where TService : notnull
    {
        var module = GetModuleOf(requestType);
        return services.GetKeyedService<TService>(module)
            ?? throw new InvalidOperationException(
                $"Module '{module}' has no keyed {typeof(TService).Name} registered, required by '{requestType.Name}'.");
    }
}
