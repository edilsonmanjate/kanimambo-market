using Kanimambo.Catalog.Domain;

namespace Kanimambo.Catalog.Application.Imports;

/// <summary>Persist step: upserts a batch of drafts by (seller, SKU) and releases tracked entities.</summary>
public interface ICatalogBatchWriter
{
    Task<BatchWriteResult> WriteAsync(SellerId sellerId, IReadOnlyList<ProductDraft> drafts, CancellationToken cancellationToken);
}
