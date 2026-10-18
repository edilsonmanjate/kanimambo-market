using System.Collections.Concurrent;

namespace Kanimambo.Simulators.Courier;

/// <summary>Every booking, by our customer's reference: booking the same reference again returns the same waybill.</summary>
public sealed class Waybills
{
    private readonly ConcurrentDictionary<string, Waybill> _byReference = new();
    private readonly ConcurrentDictionary<string, int> _requests = new();

    public static Scenario ScenarioFor(string phone) => phone.Length < 4 ? Scenario.Success : phone[^4..] switch
    {
        "0101" => Scenario.RecipientAbsent,
        "0102" => Scenario.Refused,
        "0103" => Scenario.Outage,
        _ => Scenario.Success,
    };

    /// <summary>Counts the booking request; true when it should be answered with a 503 (first request of the outage scenario).</summary>
    public bool SimulateOutage(string reference, Scenario scenario) =>
        _requests.AddOrUpdate(reference, 1, (_, n) => n + 1) == 1 && scenario == Scenario.Outage;

    public int RequestsFor(string reference) => _requests.GetValueOrDefault(reference);

    /// <summary>The waybill for the reference, and whether this request created it.</summary>
    public (Waybill Waybill, bool Created) Open(string reference, Func<Waybill> create)
    {
        var created = false;
        var waybill = _byReference.GetOrAdd(reference, _ =>
        {
            created = true;
            return create();
        });
        return (waybill, created);
    }
}
