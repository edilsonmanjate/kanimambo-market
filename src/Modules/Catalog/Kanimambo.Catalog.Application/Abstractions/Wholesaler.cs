namespace Kanimambo.Catalog.Application.Abstractions;

/// <summary>One product as published by the wholesaler's catalog API.</summary>
public sealed record WholesalerProduct(
    string Code,
    string Description,
    string Category,
    string UnitCode,
    long PriceCents,
    string Currency,
    int Stock,
    int WeightGrams,
    string TaxCategory);

/// <summary>
/// Iterator over the wholesaler's paginated, slow catalog API. Pages are fetched lazily as the
/// caller advances, so the full catalog is never held in memory and the caller can stop at any time.
/// </summary>
public interface IWholesalerCatalog
{
    IAsyncEnumerable<WholesalerProduct> StreamProductsAsync(CancellationToken cancellationToken);
}
