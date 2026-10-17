using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Imports;

/// <summary>
/// Template Method for seller catalog imports. <see cref="ImportAsync"/> fixes the algorithm —
/// <c>Read → Validate → Map → Persist → Report</c> — while each format (CSV, Excel, XML, wholesaler API)
/// only supplies the steps that differ: how records are read, format-specific checks, and how a record
/// maps to product details. Rules every import must respect (known tax category, no duplicate SKU in
/// the same file, domain invariants, batching) live here once.
/// </summary>
public abstract class CatalogImporter<TRecord>(ICatalogBatchWriter writer, ITaxRateTable taxRates) : ICatalogImporter
{
    public const int BatchSize = 200;

    public abstract ImportFormat Format { get; }

    /// <summary>The template method. Not virtual: formats cannot reorder or skip steps.</summary>
    public async Task<ImportReport> ImportAsync(SellerId sellerId, ImportSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var report = new ImportReportBuilder(Format, source.Name);
        var batch = new List<ProductDraft>(BatchSize);
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var record in ReadAsync(source, report, cancellationToken))
        {
            report.CountRow();

            var reasons = ValidateRecord(record.Value).ToList();
            if (reasons.Count == 0)
            {
                var mapped = Map(record.Value);
                if (mapped.IsFailure)
                {
                    reasons.AddRange(Describe(mapped.Error));
                }
                else
                {
                    reasons.AddRange(ValidateCommonRules(mapped.Value, seenSkus));
                    if (reasons.Count == 0)
                    {
                        batch.Add(new ProductDraft(record.Line, mapped.Value));
                    }
                }
            }

            if (reasons.Count > 0)
            {
                report.Reject(record.Line, record.Reference, reasons);
            }

            if (batch.Count == BatchSize)
            {
                await PersistAsync(sellerId, batch, report, cancellationToken);
            }
        }

        if (batch.Count > 0 && report.FatalError is null)
        {
            await PersistAsync(sellerId, batch, report, cancellationToken);
        }

        return report.Build();
    }

    /// <summary>Step 1 (required): stream raw records. Report unrecoverable problems with <see cref="ImportReportBuilder.Fail"/>.</summary>
    protected abstract IAsyncEnumerable<SourceRecord<TRecord>> ReadAsync(
        ImportSource source, ImportReportBuilder report, CancellationToken cancellationToken);

    /// <summary>Step 2 (hook): checks that only make sense for this format. None by default.</summary>
    protected virtual IEnumerable<string> ValidateRecord(TRecord record) => [];

    /// <summary>Step 3 (required): translate the format's conventions into product details.</summary>
    protected abstract Result<ProductDetails> Map(TRecord record);

    private IEnumerable<string> ValidateCommonRules(ProductDetails details, HashSet<string> seenSkus)
    {
        foreach (var invariant in SimpleProduct.Validate(details))
        {
            yield return $"{invariant.Field}: {invariant.Message}";
        }

        if (!seenSkus.Add(details.Sku))
        {
            yield return $"SKU '{details.Sku}' appears more than once in this import.";
        }

        if (taxRates.RateFor(details.TaxCategory).IsFailure)
        {
            yield return $"Unknown tax category '{details.TaxCategory.Code}'.";
        }
    }

    /// <summary>Step 4 (fixed): batched upsert keeps memory flat for large catalogs.</summary>
    private async Task PersistAsync(SellerId sellerId, List<ProductDraft> batch, ImportReportBuilder report, CancellationToken cancellationToken)
    {
        report.Add(await writer.WriteAsync(sellerId, [.. batch], cancellationToken));
        batch.Clear();
    }

    protected static IEnumerable<string> Describe(Error error) =>
        error is ValidationError validation
            ? validation.Errors.Select(e => $"{e.Field}: {e.Message}")
            : [error.Message];
}
