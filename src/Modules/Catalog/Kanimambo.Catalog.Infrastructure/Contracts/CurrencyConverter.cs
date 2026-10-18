using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Contracts;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Catalog.Infrastructure.Contracts;

internal sealed class CurrencyConverter(IExchangeRateProvider exchangeRates, TimeProvider timeProvider) : ICurrencyConverter
{
    public Task<Result<IExchangeRate>> GetRateToMznAsync(Currency currency, CancellationToken cancellationToken) =>
        exchangeRates.GetRateAsync(currency, Currency.Mzn, MozambiqueTime.Today(timeProvider), cancellationToken);
}
