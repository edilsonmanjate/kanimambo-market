using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.Infrastructure.Persistence;

/// <summary>
/// For modules without a database of their own (Operations): the command's work saves itself in the module it
/// touches, so there is no transaction to open here.
/// </summary>
public sealed class PassThroughUnitOfWork : IUnitOfWork
{
    public Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, Func<TResult, bool> commitWhen, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return operation();
    }
}
