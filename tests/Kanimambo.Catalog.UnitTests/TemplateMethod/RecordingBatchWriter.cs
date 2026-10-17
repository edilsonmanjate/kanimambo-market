using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Domain;

namespace Kanimambo.Catalog.UnitTests.TemplateMethod;

/// <summary>Persist-step stand-in: records every batch and treats every draft as a new product.</summary>
internal sealed class RecordingBatchWriter : ICatalogBatchWriter
{
    public List<IReadOnlyList<ProductDraft>> Batches { get; } = [];

    public IEnumerable<ProductDraft> All => Batches.SelectMany(b => b);

    public Task<BatchWriteResult> WriteAsync(SellerId sellerId, IReadOnlyList<ProductDraft> drafts, CancellationToken cancellationToken)
    {
        Batches.Add(drafts);
        return Task.FromResult(new BatchWriteResult(drafts.Count, 0, []));
    }
}
