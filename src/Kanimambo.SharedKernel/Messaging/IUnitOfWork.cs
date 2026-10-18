namespace Kanimambo.SharedKernel.Messaging;

/// <summary>
/// Transaction boundary of one module. Each module registers its own implementation as a keyed
/// service (key = module name) over its own DbContext, so a command only ever commits the
/// module it belongs to.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="operation"/> inside a transaction and commits only when
    /// <paramref name="commitWhen"/> returns <see langword="true"/> for its result;
    /// otherwise every change is rolled back.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        Func<TResult, bool> commitWhen,
        CancellationToken cancellationToken);
}
