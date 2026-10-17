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

/// <summary>
/// Records which <see cref="IIdempotentCommand"/> keys a module already processed. Writes are
/// enlisted in the module's <see cref="IUnitOfWork"/>, so a key is only stored when the command's
/// own changes are committed (a unique key constraint resolves concurrent duplicates).
/// </summary>
public interface IInboxStore
{
    Task<bool> HasBeenProcessedAsync(Guid idempotencyKey, CancellationToken cancellationToken);

    void MarkAsProcessed(Guid idempotencyKey, string commandName);
}
