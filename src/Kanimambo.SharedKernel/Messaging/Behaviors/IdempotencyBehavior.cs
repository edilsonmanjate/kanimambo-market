using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Kanimambo.SharedKernel.Messaging.Behaviors;

/// <summary>
/// Inbox-style de-duplication for <see cref="IIdempotentCommand"/>s that may be delivered more than once
/// (outbox relay, mobile-money callbacks). A duplicate returns success without running the handler.
/// Registered after <see cref="UnitOfWorkBehavior{TRequest, TResponse}"/> so the processed key is
/// committed atomically with the command's own changes. HTTP retries are handled separately by the
/// Idempotency-Key middleware in the API host.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IServiceProvider services,
    ModuleRegistry modules,
    ILogger<IdempotencyBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IIdempotentCommand
    where TResponse : Result
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var inbox = modules.GetModuleService<IInboxStore>(typeof(TRequest), services);

        if (await inbox.HasBeenProcessedAsync(request.IdempotencyKey, cancellationToken))
        {
            MediatorLog.DuplicateSkipped(logger, typeof(TRequest).Name, request.IdempotencyKey);

            // IIdempotentCommand : ICommand, so TResponse is always Result here.
            return (TResponse)Result.Success();
        }

        var response = await next();
        if (response.IsSuccess)
        {
            inbox.MarkAsProcessed(request.IdempotencyKey, typeof(TRequest).Name);
        }

        return response;
    }
}
