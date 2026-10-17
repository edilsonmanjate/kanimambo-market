using System.Runtime.CompilerServices;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Imports;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.Catalog.Domain.Variants;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Infrastructure.Imports;

/// <summary>
/// Imports straight from the wholesaler's paginated API. The Read step is the <see cref="IWholesalerCatalog"/>
/// iterator: pages are fetched only as the template consumes records, so memory stays flat. Format specifics:
/// prices in cents, weights in grams, packaging as wholesaler unit codes.
/// </summary>
internal sealed class WholesalerApiImporter(IWholesalerCatalog wholesaler, ICatalogBatchWriter writer, ITaxRateTable taxRates)
    : CatalogImporter<WholesalerProduct>(writer, taxRates)
{
    public override ImportFormat Format => ImportFormat.WholesalerApi;

    protected override async IAsyncEnumerable<SourceRecord<WholesalerProduct>> ReadAsync(
        ImportSource source, ImportReportBuilder report, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var products = wholesaler.StreamProductsAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        var position = 0;

        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await products.MoveNextAsync();
            }
            catch (HttpRequestException exception)
            {
                report.Fail($"Wholesaler API failed after {position} products: {exception.Message}");
                yield break;
            }

            if (!hasNext)
            {
                yield break;
            }

            position++;
            yield return new SourceRecord<WholesalerProduct>(position, products.Current.Code, products.Current);
        }
    }

    protected override IEnumerable<string> ValidateRecord(WholesalerProduct record)
    {
        if (record.PriceCents <= 0)
        {
            yield return "Price must be positive.";
        }
    }

    protected override Result<ProductDetails> Map(WholesalerProduct record)
    {
        var variant = WholesalerUnitCodes.Parse(record.UnitCode, VariantAttributesPool.Shared);
        var currency = Currency.FromCode(record.Currency);
        var taxCategory = TaxCategory.From(record.TaxCategory);

        var failure = new Result[] { variant, currency, taxCategory }.FirstOrDefault(r => r.IsFailure);
        if (failure is not null)
        {
            return failure.Error;
        }

        return new ProductDetails(
            record.Code,
            record.Description,
            record.Category,
            variant.Value,
            Money.Of(record.PriceCents / 100m, currency.Value),
            record.Stock,
            record.WeightGrams / 1000m,
            taxCategory.Value);
    }
}
