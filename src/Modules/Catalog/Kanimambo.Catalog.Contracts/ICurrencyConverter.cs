using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Contracts;

public interface ICurrencyConverter
{
    /// <summary>
    /// Today's rate to MZN. For MZN itself this is <see cref="NoExchangeRate"/> (identity, no upstream call),
    /// so callers convert every amount the same way.
    /// </summary>
    Task<Result<IExchangeRate>> GetRateToMznAsync(Currency currency, CancellationToken cancellationToken);
}
