using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

/// <summary>
/// Upserts a batch by (seller, SKU), flushes it inside the command's transaction and then clears the change
/// tracker, so a 50 000-row import keeps the same memory footprint as a 200-row one.
/// </summary>
internal sealed class CatalogBatchWriter(CatalogDbContext db) : ICatalogBatchWriter
{
    public async Task<BatchWriteResult> WriteAsync(SellerId sellerId, IReadOnlyList<ProductDraft> drafts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        var skus = drafts.Select(d => d.Details.Sku.Trim()).ToArray();
        var existing = await db.CatalogItems
            .Where(i => i.SellerId == sellerId && skus.Contains(i.Sku))
            .ToDictionaryAsync(i => i.Sku, StringComparer.Ordinal, cancellationToken);

        int created = 0, updated = 0;
        var rejected = new List<RejectedRow>();

        foreach (var draft in drafts)
        {
            var sku = draft.Details.Sku.Trim();
            Result outcome;

            switch (existing.GetValueOrDefault(sku))
            {
                case SimpleProduct product:
                    outcome = product.Update(draft.Details);
                    updated += outcome.IsSuccess ? 1 : 0;
                    break;
                case Bundle:
                    outcome = Error.Conflict("catalog.sku_is_bundle", $"SKU '{sku}' is a bundle and cannot be overwritten by an import.");
                    break;
                default:
                    var newProduct = SimpleProduct.Create(ProductId.New(), sellerId, draft.Details);
                    if (newProduct.IsSuccess)
                    {
                        db.CatalogItems.Add(newProduct.Value);
                        created++;
                    }

                    outcome = newProduct;
                    break;
            }

            if (outcome.IsFailure)
            {
                rejected.Add(new RejectedRow(draft.Line, sku, [outcome.Error.Message]));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        return new BatchWriteResult(created, updated, rejected);
    }
}
