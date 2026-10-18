using Kanimambo.Catalog.Contracts;
using Kanimambo.Promotions.Contracts;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Application.Pricing;

public sealed class PriceCartHandler(
    ICartRepository carts,
    ICatalogReader catalog,
    ICurrencyConverter currencies,
    IPromotionEvaluator promotions,
    ICurrentActor actor) : IRequestHandler<PriceCart, Result<CartPricingDto>>
{
    public async ValueTask<Result<CartPricingDto>> Handle(PriceCart request, CancellationToken cancellationToken)
    {
        var loaded = await CartAccess.LoadAsync(carts, actor, request.CartId, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded.Error;
        }

        var cart = loaded.Value;
        var items = (await catalog.GetSellableItemsAsync([.. cart.Lines.Select(l => l.ProductId)], cancellationToken))
            .ToDictionary(i => i.ProductId);

        var rates = new Dictionary<Currency, IExchangeRate>();
        foreach (var currency in items.Values.Select(i => i.Price.Currency).Distinct())
        {
            var rate = await currencies.GetRateToMznAsync(currency, cancellationToken);
            if (rate.IsFailure)
            {
                return rate.Error;
            }

            rates[currency] = rate.Value;
        }

        var lines = CartPricing.PriceLines(cart, items, rates);
        var available = lines.Where(l => l.Availability != LineAvailability.Unavailable).ToList();

        var promotion = await promotions.BestPromotionAsync(new PromotionContext(
            CartPricing.Subtotal(lines),
            available.Sum(l => l.Quantity),
            [.. available.Select(l => items[l.ProductId].Category).Distinct()],
            cart.DeliveryCity,
            cart.Customer.Type,
            request.PaymentMethod), cancellationToken);

        var priced = CartPricing.Complete(cart.Customer, lines, promotion);

        return new CartPricingDto(
            cart.Id.Value,
            cart.Version,
            priced.Customer.Type,
            cart.DeliveryCity,
            request.PaymentMethod.ToString(),
            [.. priced.Lines.Select(l => new PricedLineDto(l.ProductId, l.Sku, l.Name, l.Quantity, l.UnitPrice.Amount, l.UnitPrice.Currency.Code,
                l.Rate.Rate, l.LineTotalMzn.Amount, l.Availability.ToString()))],
            priced.Subtotal.Amount,
            new AppliedPromotionDto(priced.Promotion.PromotionId, priced.Promotion.Name, priced.Promotion.Discount.Amount),
            priced.Total.Amount,
            Currency.Mzn.Code);
    }
}
