using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Abstractions;

/// <summary>
/// Daily exchange rate (e.g. USD → MZN). Implemented by the HTTP client to the rates API (the real subject)
/// and by the caching / rate-limiting proxy that stands in front of it with the same interface.
/// </summary>
public interface IExchangeRateProvider
{
    Task<Result<IExchangeRate>> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken cancellationToken);
}
