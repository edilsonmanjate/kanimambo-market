using System.Globalization;
using System.Text.Json.Serialization;
using Kanimambo.Catalog.Domain;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Backoffice;

/// <summary>
/// "Adjust all prices of a seller by N %" (e.g. after a metical devaluation). Execute remembers every old
/// price; Undo restores them only if nobody changed those prices in between, otherwise it refuses.
/// </summary>
public sealed class AdjustSellerPricesOperation : IBackofficeOperation
{
    public const string KindName = "adjust-seller-prices";

    public required Guid SellerId { get; init; }

    public required decimal Percentage { get; init; }

    public required string Reason { get; init; }

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public IList<PriceChange> Changes { get; } = [];

    [JsonIgnore]
    public string Kind => KindName;

    public string Describe() =>
        string.Create(CultureInfo.InvariantCulture, $"Adjust prices of seller {SellerId} by {Percentage:+0.##;-0.##}% ({Changes.Count} products): {Reason}");

    public async Task<Result> ExecuteAsync(BackofficeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Changes.Count > 0)
        {
            return Error.Conflict("catalog.operation_already_executed", "This operation was already executed.");
        }

        var seller = await context.Sellers.GetAsync(new SellerId(SellerId), cancellationToken);
        if (seller is null)
        {
            return CatalogErrors.SellerNotFound(SellerId);
        }

        var products = await context.Products.GetSimpleProductsAsync(seller.Id, cancellationToken);
        if (products.Count == 0)
        {
            return Error.BusinessRule("catalog.seller_has_no_products", $"Seller '{seller.Name}' has no products to adjust.");
        }

        var factor = 1m + (Percentage / 100m);
        foreach (var product in products)
        {
            var oldPrice = product.UnitPrice;
            var changed = product.ChangePrice(oldPrice * factor);
            if (changed.IsFailure)
            {
                return changed;
            }

            Changes.Add(new PriceChange(product.Id.Value, product.Sku, oldPrice.Amount, product.UnitPrice.Amount, oldPrice.Currency.Code));
        }

        return Result.Success();
    }

    public async Task<Result> UndoAsync(BackofficeContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var products = (await context.Products.GetSimpleProductsAsync(Changes.Select(c => new ProductId(c.ProductId)).ToArray(), cancellationToken))
            .ToDictionary(p => p.Id.Value);

        var modifiedSince = Changes
            .Where(c => !products.TryGetValue(c.ProductId, out var p) || p.UnitPrice.Amount != c.NewAmount || p.UnitPrice.Currency.Code != c.Currency)
            .Select(c => c.Sku)
            .ToList();

        if (modifiedSince.Count > 0)
        {
            return Error.Conflict(
                "catalog.undo_conflict",
                $"Cannot undo: prices changed since this operation for {string.Join(", ", modifiedSince)}.");
        }

        foreach (var change in Changes)
        {
            var currency = Currency.FromCode(change.Currency).Value;
            var reverted = products[change.ProductId].ChangePrice(Money.Of(change.OldAmount, currency));
            if (reverted.IsFailure)
            {
                return reverted;
            }
        }

        return Result.Success();
    }
}
