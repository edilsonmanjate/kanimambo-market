namespace Kanimambo.SharedKernel.Messaging;

internal abstract class RequestHandlerWrapper<TResponse>
{
    public abstract ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider services, CancellationToken cancellationToken);
}
