using Kanimambo.Cart.Domain;
using Kanimambo.Catalog.Contracts;
using Kanimambo.Promotions.Contracts;
using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Cart.Application.Pricing;

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
