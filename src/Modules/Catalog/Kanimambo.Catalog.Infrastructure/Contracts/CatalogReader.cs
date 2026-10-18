using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Contracts;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Infrastructure.Persistence;
using Kanimambo.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Contracts;

/// <summary>Answers other modules from the search read model, so bundle stock and price are already derived.</summary>
internal sealed class CatalogReader(CatalogDbContext db, ICatalogItemRepository items) : ICatalogReader
{
    public async Task<IReadOnlyList<CatalogItemSnapshot>> GetSellableItemsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToArray();
        var entries = await db.CatalogEntries.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);

        return entries
            .Select(e => new CatalogItemSnapshot(
                e.Id, e.SellerId, e.Sku, e.Name, e.Category,
                Money.Of(e.Price, Currency.FromCode(e.Currency).Value),
                e.AvailableStock,
                e.WeightKg))
            .ToList();
    }

    public async Task<IReadOnlyList<SellerSnapshot>> GetSellersAsync(IReadOnlyCollection<Guid> sellerIds, CancellationToken cancellationToken)
    {
        var ids = sellerIds.Distinct().Select(id => new SellerId(id)).ToArray();
        var sellers = await db.Sellers.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(cancellationToken);
        return sellers.Select(s => new SellerSnapshot(s.Id.Value, s.Name, s.IsActive, [.. s.DeliveryCities], s.MinimumOrder, s.City)).ToList();
    }

    public async Task<IReadOnlyList<ProductTaxProfile>> GetTaxProfilesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        var trees = await items.GetTreesAsync([.. productIds.Distinct().Select(id => new ProductId(id))], cancellationToken);
        return trees.Select(item =>
        {
            var amounts = item.TaxableAmounts(1).GroupBy(a => a.Category.Code).Select(g => (Category: g.Key, Amount: g.Sum(a => a.Gross.Amount))).ToList();
            var total = amounts.Sum(a => a.Amount);
            return new ProductTaxProfile(item.Id.Value, total == 0m
                ? [.. amounts.Select(a => new TaxShare(a.Category, 1m / amounts.Count))]
                : [.. amounts.OrderBy(a => a.Category, StringComparer.Ordinal).Select(a => new TaxShare(a.Category, a.Amount / total))]);
        }).ToList();
    }
}
