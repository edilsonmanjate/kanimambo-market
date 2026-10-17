using Kanimambo.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

/// <summary>Builds a real DI container with the mediator, as the API host does.</summary>
internal sealed class MediatorHost : IAsyncDisposable
{
    public const string Module = "Testing";

    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    private MediatorHost(ServiceProvider root)
    {
        _root = root;
        _scope = root.CreateAsyncScope();
    }

    public IMediator Mediator => _scope.ServiceProvider.GetRequiredService<IMediator>();

    public CallLog Log => _root.GetRequiredService<CallLog>();

    public static MediatorHost Create(Action<MediatorOptions>? behaviors = null, bool withModuleServices = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<CallLog>();

        if (withModuleServices)
        {
            services.AddKeyedScoped<IUnitOfWork, RecordingUnitOfWork>(Module);
            services.AddKeyedSingleton<IInboxStore, InMemoryInbox>(Module);
        }

        services.AddMediator(options =>
        {
            options.AddModule(Module, typeof(MediatorHost).Assembly);
            behaviors?.Invoke(options);
        });

        return new MediatorHost(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }));
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
    }
}
