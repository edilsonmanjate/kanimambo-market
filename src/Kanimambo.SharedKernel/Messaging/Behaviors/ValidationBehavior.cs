using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Messaging.Behaviors;

/// <summary>
/// Runs every <see cref="IValidator{TRequest}"/> and short-circuits with a <see cref="ValidationError"/>
/// (the handler is never invoked). Only applies to requests that return a <see cref="Result"/>.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result, IResultFactory<TResponse>
{
    public ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var failures = validators.SelectMany(validator => validator.Validate(request)).ToArray();

        return failures.Length == 0
            ? next()
            : ValueTask.FromResult(TResponse.CreateFailure(new ValidationError(failures)));
    }
}
