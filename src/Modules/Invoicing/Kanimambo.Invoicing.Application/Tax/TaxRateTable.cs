using System.Collections.Frozen;
using Kanimambo.Invoicing.Contracts;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Application.Tax;

/// <summary>
/// Singleton (by DI lifetime): the IVA rate table is read on every price display, checkout and invoice line, so it
/// is loaded once and kept in memory. It is thread-safe without locks on the read path: readers use an immutable
/// snapshot, and <see cref="Load"/> (called on configuration reload) builds a new one and swaps the reference.
/// It is deliberately not a <c>static Instance</c>: see ADR 0010.
/// </summary>
public sealed class TaxRateTable : ITaxRateTable
{
    private FrozenDictionary<string, TaxRate[]> _byCategory = FrozenDictionary<string, TaxRate[]>.Empty;
    private int _version;

    public TaxRateTable(IEnumerable<TaxRate> rates) => Load(rates);

    /// <summary>How many times the table was (re)loaded — used to show that one instance serves the whole app.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Replaces the whole table atomically; an invalid table is refused and the current one stays.</summary>
    public void Load(IEnumerable<TaxRate> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        var list = rates.ToList();
        foreach (var rate in list)
        {
            if (string.IsNullOrWhiteSpace(rate.Category) || rate.Rate is < 0m or >= 1m)
            {
                throw new ArgumentException($"Invalid tax rate '{rate.Category}' = {rate.Rate}: rates are fractions between 0 and 1.", nameof(rates));
            }

            if (rate.Rate == 0m && string.IsNullOrWhiteSpace(rate.ExemptionReason))
            {
                throw new ArgumentException($"Category '{rate.Category}' is exempt but has no legal exemption reason.", nameof(rates));
            }
        }

        var snapshot = list
            .GroupBy(r => r.Category.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(g => g.Key, g => g.OrderByDescending(r => r.ValidFrom).ToArray(), StringComparer.OrdinalIgnoreCase);

        Volatile.Write(ref _byCategory, snapshot);
        Interlocked.Increment(ref _version);
    }

    public Result<TaxRate> RateFor(string category, DateOnly on)
    {
        var table = Volatile.Read(ref _byCategory);
        if (category is not null && table.TryGetValue(category, out var history))
        {
            foreach (var rate in history)
            {
                if (rate.ValidFrom <= on)
                {
                    return rate;
                }
            }
        }

        return Error.BusinessRule("tax.unknown", $"No IVA rate in force on {on:yyyy-MM-dd} for tax category '{category}'.");
    }

    public IReadOnlyList<TaxRate> RatesOn(DateOnly on)
    {
        var table = Volatile.Read(ref _byCategory);
        return [.. table.Keys.Order(StringComparer.Ordinal).Select(c => RateFor(c, on)).Where(r => r.IsSuccess).Select(r => r.Value)];
    }
}
