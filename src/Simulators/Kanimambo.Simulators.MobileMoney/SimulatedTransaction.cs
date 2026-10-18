namespace Kanimambo.Simulators.MobileMoney;

public sealed class SimulatedTransaction
{
    public required Guid Reference { get; init; }

    public required string Operator { get; init; }

    public required string Phone { get; init; }

    public required decimal Amount { get; init; }

    public required Uri CallbackUrl { get; init; }

    public required string TransactionId { get; init; }

    public TransactionState State { get; set; }

    /// <summary>Failure reason in the simulator's own words: <c>insufficient_balance</c>.</summary>
    public string? Failure { get; set; }
}
