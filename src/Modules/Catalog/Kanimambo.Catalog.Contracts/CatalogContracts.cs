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

/// <summary>What another module may know about a seller: whether it sells, where it is based and delivers, its minimum order.</summary>
public sealed record SellerSnapshot(Guid SellerId, string Name, bool IsActive, IReadOnlyList<string> DeliveryCities, Money MinimumOrder, string City);

/// <summary>Part of a product's price that falls in one tax category; a bundle can mix exempt and taxed goods.</summary>
public sealed record TaxShare(string Category, decimal Fraction);

/// <summary>How a product's price splits across tax categories (fractions add up to 1).</summary>
public sealed record ProductTaxProfile(Guid ProductId, IReadOnlyList<TaxShare> Shares);

public interface ICatalogReader
{
    /// <summary>Items that can be sold now; unknown products and products of suspended sellers are absent.</summary>
    Task<IReadOnlyList<CatalogItemSnapshot>> GetSellableItemsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<SellerSnapshot>> GetSellersAsync(IReadOnlyCollection<Guid> sellerIds, CancellationToken cancellationToken);

    /// <summary>Tax categories of products, including every component of bundles (Composite), for invoicing.</summary>
    Task<IReadOnlyList<ProductTaxProfile>> GetTaxProfilesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}

public interface ICurrencyConverter
{
    /// <summary>
    /// Today's rate to MZN. For MZN itself this is <see cref="NoExchangeRate"/> (identity, no upstream call),
    /// so callers convert every amount the same way.
    /// </summary>
    Task<Result<IExchangeRate>> GetRateToMznAsync(Currency currency, CancellationToken cancellationToken);
}
