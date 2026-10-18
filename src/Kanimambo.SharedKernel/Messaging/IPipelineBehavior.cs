namespace Kanimambo.SharedKernel.Messaging;

/// <summary>
/// Cross-cutting step wrapped around every matching request. Which requests a behavior
/// applies to is expressed with generic constraints, not runtime type checks.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken);
}
