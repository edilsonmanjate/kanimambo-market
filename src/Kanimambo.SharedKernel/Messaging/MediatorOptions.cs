using System.Reflection;

namespace Kanimambo.SharedKernel.Messaging;

public sealed class MediatorOptions
{
    private readonly Dictionary<Assembly, string> _modules = [];
    private readonly List<Type> _behaviors = [];

    internal IReadOnlyDictionary<Assembly, string> Modules => _modules;

    internal IReadOnlyList<Type> Behaviors => _behaviors;

    /// <summary>
    /// Registers every handler and validator found in <paramref name="assemblies"/> and declares that
    /// the requests in them belong to <paramref name="moduleName"/> (used to pick the module's unit of work).
    /// </summary>
    public MediatorOptions AddModule(string moduleName, params Assembly[] assemblies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            if (_modules.TryGetValue(assembly, out var existing) && existing != moduleName)
            {
                throw new InvalidOperationException(
                    $"Assembly '{assembly.GetName().Name}' is already registered for module '{existing}'.");
            }

            _modules[assembly] = moduleName;
        }

        return this;
    }

    /// <summary>
    /// Adds an open generic pipeline behavior. Behaviors run in the order they are added:
    /// the first one added is the outermost.
    /// </summary>
    public MediatorOptions AddBehavior(Type openGenericBehaviorType)
    {
        ArgumentNullException.ThrowIfNull(openGenericBehaviorType);

        var implementsBehavior = openGenericBehaviorType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (!openGenericBehaviorType.IsGenericTypeDefinition || !implementsBehavior)
        {
            throw new ArgumentException(
                $"'{openGenericBehaviorType.Name}' must be an open generic type implementing IPipelineBehavior<,>.",
                nameof(openGenericBehaviorType));
        }

        if (_behaviors.Contains(openGenericBehaviorType))
        {
            throw new InvalidOperationException($"Behavior '{openGenericBehaviorType.Name}' is already registered.");
        }

        _behaviors.Add(openGenericBehaviorType);
        return this;
    }
}
