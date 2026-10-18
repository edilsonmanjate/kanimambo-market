using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Application.Pricing;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Products;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;

namespace Kanimambo.Catalog.Application.Products;

/// <summary>Uses the Composite uniformly: the same calls work for a single product and for nested bundles.</summary>
public sealed class GetProductDetailsHandler(
    ICatalogItemRepository products,
    ITaxRateTable taxRates,
    IExchangeRateProvider exchangeRates,
    TimeProvider timeProvider) : IRequestHandler<GetProductDetails, Result<ProductDetailsDto>>
{
    public async ValueTask<Result<ProductDetailsDto>> Handle(GetProductDetails request, CancellationToken cancellationToken)
    {
        var item = await products.GetTreeAsync(new ProductId(request.ProductId), cancellationToken);
        if (item is null)
        {
            return CatalogErrors.ProductNotFound(request.ProductId);
        }

        var iva = IvaBreakdown.Calculate(item.TaxableAmounts(1), taxRates);
        if (iva.IsFailure)
        {
            return iva.Error;
        }

        var rate = await exchangeRates.GetRateAsync(item.Price.Currency, Currency.Mzn, MozambiqueTime.Today(timeProvider), cancellationToken);

        return new ProductDetailsDto(
            item.Id.Value,
            item.SellerId.Value,
            item.Sku,
            item.Name,
            item.Category,
            KindOf(item),
            (item as SimpleProduct)?.Variant.Describe(),
            MoneyDto.From(item.Price),
            rate.IsSuccess ? MoneyDto.From(rate.Value.Convert(item.Price)) : null,
            rate.IsSuccess ? rate.Value.Rate : null,
            item.WeightKg,
            item.AvailableStock,
            new IvaBreakdownDto(
                iva.Value.Lines.Select(l => new IvaLineDto(l.Category, l.Rate, MoneyDto.From(l.Net), MoneyDto.From(l.Iva))).ToList(),
                MoneyDto.From(iva.Value.Net),
                MoneyDto.From(iva.Value.Iva),
                MoneyDto.From(iva.Value.Gross)),
            ComponentsOf(item));
    }

    internal static string KindOf(CatalogItem item) => item is Bundle ? "Bundle" : "Simple";

    private static List<ComponentDto> ComponentsOf(CatalogItem item) =>
        item is Bundle bundle
            ? bundle.Components
                .Select(c => new ComponentDto(c.ComponentId.Value, c.Component.Sku, c.Component.Name, KindOf(c.Component), c.Quantity, ComponentsOf(c.Component)))
                .ToList()
            : [];
}
