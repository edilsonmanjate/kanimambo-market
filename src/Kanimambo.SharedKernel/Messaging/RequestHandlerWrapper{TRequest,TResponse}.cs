using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.SharedKernel.Messaging;

internal sealed class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException($"No handler registered for '{typeof(TRequest).FullName}'.");

        RequestHandlerNext<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

        // Registration order == execution order: the first registered behavior is the outermost,
        // so the chain is built from the last behavior inwards.
        var behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
        }

        return pipeline();
    }
}
