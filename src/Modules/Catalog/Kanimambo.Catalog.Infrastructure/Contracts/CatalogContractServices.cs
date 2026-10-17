using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Contracts;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Contracts;

/// <summary>Answers other modules from the search read model, so bundle stock and price are already derived.</summary>
internal sealed class CatalogReader(CatalogDbContext db) : ICatalogReader
{
    public async Task<IReadOnlyList<CatalogItemSnapshot>> GetSellableItemsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var entries = await db.CatalogEntries.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);

        return entries
            .Select(e => new CatalogItemSnapshot(
                e.Id, e.SellerId, e.Sku, e.Name, e.Category,
                Money.Of(e.Price, Currency.FromCode(e.Currency).Value),
                e.AvailableStock))
            .ToList();
    }
}

internal sealed class CurrencyConverter(IExchangeRateProvider exchangeRates, TimeProvider timeProvider) : ICurrencyConverter
{
    public Task<Result<IExchangeRate>> GetRateToMznAsync(Currency currency, CancellationToken cancellationToken) =>
        exchangeRates.GetRateAsync(currency, Currency.Mzn, MozambiqueTime.Today(timeProvider), cancellationToken);
}
