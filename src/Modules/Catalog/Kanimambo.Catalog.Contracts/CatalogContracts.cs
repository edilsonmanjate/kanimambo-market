using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Contracts;

/// <summary>What another module may know about a sellable item right now (price, stock derived for bundles).</summary>
public sealed record CatalogItemSnapshot(
    Guid ProductId,
    Guid SellerId,
    string Sku,
    string Name,
    string Category,
    Money Price,
    int AvailableStock,
    decimal WeightKg);

/// <summary>What another module may know about a seller: whether it sells, where it delivers, its minimum order.</summary>
public sealed record SellerSnapshot(Guid SellerId, string Name, bool IsActive, IReadOnlyList<string> DeliveryCities, Money MinimumOrder);

public interface ICatalogReader
{
    /// <summary>Items that can be sold now; unknown products and products of suspended sellers are absent.</summary>
    Task<IReadOnlyList<CatalogItemSnapshot>> GetSellableItemsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<SellerSnapshot>> GetSellersAsync(IReadOnlyCollection<Guid> sellerIds, CancellationToken cancellationToken);
}

public interface ICurrencyConverter
{
    /// <summary>
    /// Today's rate to MZN. For MZN itself this is <see cref="NoExchangeRate"/> (identity, no upstream call),
    /// so callers convert every amount the same way.
    /// </summary>
    Task<Result<IExchangeRate>> GetRateToMznAsync(Currency currency, CancellationToken cancellationToken);
}
