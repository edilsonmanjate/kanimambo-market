using System.Collections.Concurrent;

namespace Kanimambo.SharedKernel.Domain;

/// <summary>Converts amounts from one currency to another at a given rate.</summary>
public interface IExchangeRate
{
    Currency From { get; }

    Currency To { get; }

    decimal Rate { get; }

    Money Convert(Money amount);
}

/// <summary>
/// Null Object for prices that are already in the target currency (most of the catalog is in MZN).
/// Converting through it is the identity, so pricing code converts every line the same way instead of
/// branching on "is this price already in meticais?" — and no upstream rate is ever requested for it.
/// </summary>
public sealed class NoExchangeRate : IExchangeRate
{
    private static readonly ConcurrentDictionary<Currency, NoExchangeRate> Instances = new();

    private NoExchangeRate(Currency currency) => From = To = currency;

    public Currency From { get; }

    public Currency To { get; }

    public decimal Rate => 1m;

    public static NoExchangeRate For(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return Instances.GetOrAdd(currency, static c => new NoExchangeRate(c));
    }

    public Money Convert(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        return amount.Currency == From
            ? amount
            : throw new InvalidOperationException($"NoExchangeRate for {From.Code} cannot convert {amount.Currency.Code}.");
    }

    public override string ToString() => $"{From.Code} → {To.Code} (no conversion)";
}
