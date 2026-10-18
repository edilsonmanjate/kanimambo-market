using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Catalog.Application.Abstractions;

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
