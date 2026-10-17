namespace Kanimambo.SharedKernel.Messaging;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>Continuation to the next behavior in the pipeline, or to the handler itself.</summary>
public delegate ValueTask<TResponse> RequestHandlerNext<TResponse>();

/// <summary>
/// Cross-cutting step wrapped around every matching request. Which requests a behavior
/// applies to is expressed with generic constraints, not runtime type checks.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken);
}
