using Kanimambo.Catalog.Application.Search;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Backoffice;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Sellers;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Specifications;

namespace Kanimambo.Catalog.Application.Abstractions;

public interface ISellerRepository
{
    Task<Seller?> GetAsync(SellerId id, CancellationToken cancellationToken);

    void Add(Seller seller);
}

/// <summary>Repositories return materialized aggregates; <see cref="IQueryable"/> never leaves Infrastructure.</summary>
public interface ICatalogItemRepository
{
    /// <summary>Loads the item and every descendant, so Composite operations can recurse safely.</summary>
    Task<CatalogItem?> GetTreeAsync(ProductId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogItem>> GetTreesAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(SellerId sellerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SimpleProduct>> GetSimpleProductsAsync(IReadOnlyCollection<ProductId> ids, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(SellerId sellerId, string sku, CancellationToken cancellationToken);

    void Add(CatalogItem item);
}

/// <summary>Runs catalog specifications in the database and returns one page of results.</summary>
public interface ICatalogSearch
{
    Task<PagedResult<CatalogEntry>> SearchAsync(
        Specification<CatalogEntry> specification, int page, int pageSize, CancellationToken cancellationToken);
}

public interface IBackofficeOperationLog
{
    void Add(BackofficeOperationRecord record);

    Task<BackofficeOperationRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<BackofficeOperationRecord>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>IVA (and future) rates per tax category, from configuration — never hard-coded.</summary>
public interface ITaxRateTable
{
    Result<decimal> RateFor(TaxCategory category);
}
