using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Abstractions;

public sealed record ExchangeRate(Currency From, Currency To, DateOnly Date, decimal Rate)
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

/// <summary>
/// Daily exchange rate (e.g. USD → MZN). Implemented by the HTTP client to the rates API (the real subject)
/// and by the caching / rate-limiting proxy that stands in front of it with the same interface.
/// </summary>
public interface IExchangeRateProvider
{
    Task<Result<ExchangeRate>> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken cancellationToken);
}

public static class ExchangeRateProviderExtensions
{
    /// <summary>Today's rates to MZN for every supported currency; unavailable rates are simply missing.</summary>
    public static async Task<IReadOnlyDictionary<Currency, decimal>> GetRatesToMznAsync(
        this IExchangeRateProvider provider, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var rates = new Dictionary<Currency, decimal> { [Currency.Mzn] = 1m };
        foreach (var currency in Currency.Supported.Where(c => c != Currency.Mzn))
        {
            var rate = await provider.GetRateAsync(currency, Currency.Mzn, date, cancellationToken);
            if (rate.IsSuccess)
            {
                rates[currency] = rate.Value.Rate;
            }
        }

        return rates;
    }
}
