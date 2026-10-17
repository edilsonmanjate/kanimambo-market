using System.Globalization;

namespace Kanimambo.SharedKernel.Domain;

/// <summary>
/// Monetary amount, always rounded to 2 decimal places with <see cref="MidpointRounding.AwayFromZero"/>.
/// Uses <see cref="decimal"/> only. Arithmetic between different currencies is a programming error
/// and throws; conversion must be explicit through an exchange rate.
/// </summary>
public sealed record Money : IComparable<Money>
{
    public const int DecimalPlaces = 2;

    private Money(decimal amount, Currency currency)
    {
        Amount = decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public decimal Amount { get; }

    public Currency Currency { get; }

    public bool IsZero => Amount == 0m;

    public bool IsNegative => Amount < 0m;

    public static Money Of(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new Money(amount, currency);
    }

    public static Money Mzn(decimal amount) => new(amount, Currency.Mzn);

    public static Money Zero(Currency currency) => Of(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public Money Negate() => new(-Amount, Currency);

    public int CompareTo(Money? other)
    {
        if (other is null)
        {
            return 1;
        }

        EnsureSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public static Money operator +(Money left, Money right) => Required(left).Add(right);

    public static Money operator -(Money left, Money right) => Required(left).Subtract(right);

    public static Money operator -(Money money) => Required(money).Negate();

    public static Money operator *(Money money, decimal factor) => Required(money).Multiply(factor);

    public static Money operator *(decimal factor, Money money) => Required(money).Multiply(factor);

    public static bool operator <(Money left, Money right) => Required(left).CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => Required(left).CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => Required(left).CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => Required(left).CompareTo(right) >= 0;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency.Code}");

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine {Currency.Code} with {other.Currency.Code}; convert explicitly first.");
        }
    }

    private static Money Required(Money money)
    {
        ArgumentNullException.ThrowIfNull(money);
        return money;
    }
}
