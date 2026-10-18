namespace Kanimambo.SharedKernel.Messaging;

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
