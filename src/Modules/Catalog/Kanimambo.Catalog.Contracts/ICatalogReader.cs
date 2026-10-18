namespace Kanimambo.Catalog.Contracts;

public interface ICatalogReader
{
    /// <summary>Items that can be sold now; unknown products and products of suspended sellers are absent.</summary>
    Task<IReadOnlyList<CatalogItemSnapshot>> GetSellableItemsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<SellerSnapshot>> GetSellersAsync(IReadOnlyCollection<Guid> sellerIds, CancellationToken cancellationToken);

    /// <summary>Tax categories of products, including every component of bundles (Composite), for invoicing.</summary>
    Task<IReadOnlyList<ProductTaxProfile>> GetTaxProfilesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
}
