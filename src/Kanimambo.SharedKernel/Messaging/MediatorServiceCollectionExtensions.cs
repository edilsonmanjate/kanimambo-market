using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kanimambo.SharedKernel.Messaging;

public static class MediatorServiceCollectionExtensions
{
    private static readonly Type[] ScannedInterfaces =
    [
        typeof(IRequestHandler<,>),
        typeof(INotificationHandler<>),
        typeof(IValidator<>),
    ];

    public static IServiceCollection AddMediator(this IServiceCollection services, Action<MediatorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        if (services.Any(d => d.ServiceType == typeof(ModuleRegistry)))
        {
            throw new InvalidOperationException("AddMediator must be called once; declare every module in that call.");
        }

        var options = new MediatorOptions();
        configure(options);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new ModuleRegistry(options.Modules.ToDictionary()));
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        services.AddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());

        foreach (var assembly in options.Modules.Keys)
        {
            RegisterHandlers(services, assembly);
        }

        foreach (var behavior in options.Behaviors)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), behavior);
        }

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly)
    {
        var candidates = assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var implementation in candidates)
        {
            foreach (var contract in implementation.GetInterfaces().Where(IsScannedContract))
            {
                var isRequestHandler = contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
                if (isRequestHandler && services.Any(d => d.ServiceType == contract))
                {
                    throw new InvalidOperationException(
                        $"More than one handler for '{contract.GenericTypeArguments[0].FullName}'; a request has exactly one handler.");
                }

                services.AddScoped(contract, implementation);
            }
        }
    }

    private static bool IsScannedContract(Type type) =>
        type.IsGenericType && ScannedInterfaces.Contains(type.GetGenericTypeDefinition());
}
