namespace Kanimambo.SharedKernel.Operations;

/// <summary>
/// Operations view of one module's outbox: what is waiting, what was given up on (dead-lettered after
/// <c>Outbox:MaxAttempts</c>), and replaying a dead letter once the cause is fixed. One instance per publishing module.
/// </summary>
public interface IOutboxAdministration
{
    string Module { get; }

    Task<OutboxSummary> SummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DeadLetter>> ListDeadLettersAsync(int max, CancellationToken cancellationToken);

    /// <summary>
    /// Puts a dead-lettered message back in the queue with a fresh attempt count. Subscribers that already handled it
    /// are not called again. False when there is no such dead letter.
    /// </summary>
    Task<bool> ReplayAsync(Guid messageId, CancellationToken cancellationToken);
}
