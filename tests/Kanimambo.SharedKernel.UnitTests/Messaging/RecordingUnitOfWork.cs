using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.SharedKernel.UnitTests.Messaging;

/// <summary>In-memory unit of work that records whether it would have committed.</summary>
public sealed class RecordingUnitOfWork(CallLog log) : IUnitOfWork
{
    public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, Func<TResult, bool> commitWhen, CancellationToken cancellationToken)
    {
        log.Add("uow:begin");
        var result = await operation();
        log.Add(commitWhen(result) ? "uow:commit" : "uow:rollback");
        return result;
    }
}
