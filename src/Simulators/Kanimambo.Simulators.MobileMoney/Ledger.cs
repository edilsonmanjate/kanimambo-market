using System.Collections.Concurrent;

namespace Kanimambo.Simulators.MobileMoney;

/// <summary>In-memory record of every transaction, shared by the three operator endpoints.</summary>
public sealed class Ledger
{
    private readonly ConcurrentDictionary<Guid, SimulatedTransaction> _transactions = new();
    private readonly ConcurrentDictionary<Guid, int> _requests = new();

    public static Scenario ScenarioFor(string phone) => phone[^4..] switch
    {
        "0001" => Scenario.InsufficientBalance,
        "0002" => Scenario.Duplicate,
        "0003" => Scenario.NoAnswer,
        "0004" => Scenario.DelayedCallback,
        "0005" => Scenario.InvalidNumber,
        "0006" => Scenario.Outage,
        _ => Scenario.Success,
    };

    /// <summary>False when the reference was already used (a real duplicate).</summary>
    public bool TryOpen(SimulatedTransaction transaction) => _transactions.TryAdd(transaction.Reference, transaction);

    public SimulatedTransaction? Find(Guid reference) => _transactions.GetValueOrDefault(reference);

    /// <summary>Counts a payment request for the reference; returns how many arrived so far, this one included.</summary>
    public int CountRequest(Guid reference) => _requests.AddOrUpdate(reference, 1, (_, n) => n + 1);

    public int RequestsFor(Guid reference) => _requests.GetValueOrDefault(reference);

    /// <summary>Counts the request and tells whether to answer it with a 503 (outage scenario).</summary>
    public bool SimulateOutage(Guid reference, Scenario scenario) => CountRequest(reference) > 0 && scenario == Scenario.Outage;
}
