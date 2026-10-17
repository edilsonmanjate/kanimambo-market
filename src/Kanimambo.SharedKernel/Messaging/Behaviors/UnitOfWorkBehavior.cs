using Kanimambo.SharedKernel.Results;

namespace Kanimambo.SharedKernel.Messaging.Behaviors;

/// <summary>
/// Wraps commands (never queries) in the owning module's transaction. A failed <see cref="Result"/>
/// rolls back every change, so handlers never call SaveChanges themselves.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse>(IServiceProvider services, ModuleRegistry modules)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, ICommandBase
    where TResponse : Result
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerNext<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var unitOfWork = modules.GetModuleService<IUnitOfWork>(typeof(TRequest), services);

        return await unitOfWork.ExecuteAsync(
            async () => await next(),
            response => response.IsSuccess,
            cancellationToken);
    }
}
