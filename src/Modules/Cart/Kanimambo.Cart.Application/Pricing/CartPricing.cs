using Kanimambo.Cart.Domain;
using Kanimambo.Catalog.Contracts;
using Kanimambo.Promotions.Contracts;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Application.Pricing;

public enum LineAvailability
{
    Available,
    InsufficientStock,
    Unavailable,
}

public sealed record PricedLine(
    Guid ProductId,
    string Sku,
    string Name,
    int Quantity,
    Money UnitPrice,
    IExchangeRate Rate,
    Money LineTotalMzn,
    LineAvailability Availability);

/// <summary>A priced cart. Customer, promotion and exchange rates are always objects — null objects when "none".</summary>
public sealed record PricedCart(
    Customer Customer,
    IReadOnlyList<PricedLine> Lines,
    Money Subtotal,
    IAppliedPromotion Promotion,
    Money Total);

/// <summary>
/// Prices a cart in MZN: lines converted with their currency's rate (<see cref="NoExchangeRate"/> for MZN),
/// then the best promotion (<see cref="NoPromotion"/> when none applies), for any customer
/// (<see cref="GuestCustomer"/> when anonymous). There is no "if missing" branch in this flow.
/// </summary>
public static class CartPricing
{
    public static IReadOnlyList<PricedLine> PriceLines(
        ShoppingCart cart,
        IReadOnlyDictionary<Guid, CatalogItemSnapshot> items,
        IReadOnlyDictionary<Currency, IExchangeRate> rates)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(rates);

        return cart.Lines.Select(line => items.TryGetValue(line.ProductId, out var item)
                ? Priced(line, item, rates[item.Price.Currency])
                : Unavailable(line))
            .ToList();
    }

    public static PricedCart Complete(Customer customer, IReadOnlyList<PricedLine> lines, IAppliedPromotion promotion)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(promotion);

        var subtotal = Subtotal(lines);
        return new PricedCart(customer, lines, subtotal, promotion, subtotal - promotion.Discount);
    }

    public static Money Subtotal(IEnumerable<PricedLine> lines) =>
        lines.Where(l => l.Availability != LineAvailability.Unavailable)
            .Select(l => l.LineTotalMzn)
            .Aggregate(Money.Zero(Currency.Mzn), (total, next) => total + next);

    private static PricedLine Priced(CartLine line, CatalogItemSnapshot item, IExchangeRate rate) =>
        new(line.ProductId, item.Sku, item.Name, line.Quantity, item.Price, rate,
            rate.Convert(item.Price * line.Quantity),
            line.Quantity <= item.AvailableStock ? LineAvailability.Available : LineAvailability.InsufficientStock);

    private static PricedLine Unavailable(CartLine line) =>
        new(line.ProductId, line.Sku, line.Name, line.Quantity, Money.Zero(Currency.Mzn), NoExchangeRate.For(Currency.Mzn),
            Money.Zero(Currency.Mzn), LineAvailability.Unavailable);
}

public sealed record PriceCart(Guid CartId, PaymentMethod PaymentMethod = PaymentMethod.None) : IQuery<CartPricingDto>;

public sealed record PricedLineDto(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice, string Currency, decimal ExchangeRate, decimal LineTotalMzn, string Availability);

public sealed record AppliedPromotionDto(Guid Id, string Name, decimal Discount);

public sealed record CartPricingDto(
    Guid CartId,
    int Version,
    string CustomerType,
    string DeliveryCity,
    string PaymentMethod,
    IReadOnlyList<PricedLineDto> Lines,
    decimal Subtotal,
    AppliedPromotionDto Promotion,
    decimal Total,
    string Currency);

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
