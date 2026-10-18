using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Application.Abstractions;

public sealed record ExchangeRate(Currency From, Currency To, DateOnly Date, decimal Rate) : IExchangeRate
{
    public Money Convert(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        if (amount.Currency != From)
        {
            throw new InvalidOperationException($"Rate converts {From.Code}, not {amount.Currency.Code}.");
        }

        return Money.Of(amount.Amount * Rate, To);
    }
}
