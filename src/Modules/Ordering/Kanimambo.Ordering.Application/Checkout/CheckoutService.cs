using Kanimambo.Cart.Contracts;
using Kanimambo.Catalog.Contracts;
using Kanimambo.Ordering.Application.Acceptance;
using Kanimambo.Ordering.Contracts;
using Kanimambo.Ordering.Domain;
using Kanimambo.Promotions.Contracts;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Contracts;

namespace Kanimambo.Ordering.Application.Checkout;

internal sealed class CheckoutService(
    ICartReader carts,
    IDraftOrderRepository drafts,
    ICatalogReader catalog,
    ICurrencyConverter currencies,
    IPromotionEvaluator promotions,
    IShippingQuoter shipping,
    IOrderAcceptanceChain acceptance,
    IOrderRepository orders,
    IOrderNumbers numbers,
    ICurrentActor actor,
    TimeProvider timeProvider) : ICheckoutService
{
    public async Task<Result<CheckoutResult>> CheckoutCartAsync(Guid cartId, CheckoutDetails details, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(details);

        // Idempotency first: the same cart always yields the same order, however often checkout is retried.
        if (await orders.FindBySourceAsync(CheckoutSources.Cart, cartId, cancellationToken) is { } existing)
        {
            return ToResult(existing, alreadyPlaced: true);
        }

        var cart = await carts.GetForCheckoutAsync(cartId, cancellationToken);
        if (cart.IsFailure)
        {
            return cart.Error;
        }

        if (cart.Value.CheckedOutOrderId is not null)
        {
            return Error.Conflict("ordering.cart_checked_out", "This cart was already checked out.");
        }

        var customer = new OrderCustomer(cart.Value.CustomerId, cart.Value.CustomerType, details.Phone.Trim(), details.Nuit?.Trim() ?? string.Empty);
        return await PlaceAsync(CheckoutSources.Cart, cartId, customer, cart.Value.DeliveryCity,
            [.. cart.Value.Lines.Select(l => new BasketLine(l.ProductId, l.Quantity))], details.PaymentMethod, details.Delivery, cancellationToken);
    }

    public async Task<Result<CheckoutResult>> SubmitDraftAsync(Guid draftId, CheckoutDetails details, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (await orders.FindBySourceAsync(CheckoutSources.Draft, draftId, cancellationToken) is { } existing)
        {
            return ToResult(existing, alreadyPlaced: true);
        }

        var draft = await drafts.GetAsync(draftId, cancellationToken);
        if (draft is null || !OrderAccess.CanEdit(draft, actor))
        {
            return OrderingErrors.DraftNotFound(draftId);
        }

        var customer = new OrderCustomer(draft.CustomerId, OrderCustomer.BusinessType, details.Phone.Trim(), details.Nuit?.Trim() ?? string.Empty);
        var placed = await PlaceAsync(CheckoutSources.Draft, draftId, customer, draft.DeliveryCity,
            [.. draft.Lines.Select(l => new BasketLine(l.ProductId, l.Quantity))], details.PaymentMethod, details.Delivery, cancellationToken);

        if (placed.IsSuccess)
        {
            draft.MarkSubmitted(placed.Value.OrderId, timeProvider.GetUtcNow());
        }

        return placed;
    }

    private async Task<Result<CheckoutResult>> PlaceAsync(
        string source,
        Guid sourceId,
        OrderCustomer customer,
        string deliveryCity,
        IReadOnlyList<BasketLine> basket,
        PaymentMethod paymentMethod,
        DeliveryChoice delivery,
        CancellationToken cancellationToken)
    {
        if (basket.Count == 0)
        {
            return Error.BusinessRule("ordering.empty", "There is nothing to order.");
        }

        // 1. Current catalog prices and stock (never trust prices the client saw earlier).
        var items = (await catalog.GetSellableItemsAsync([.. basket.Select(b => b.ProductId)], cancellationToken)).ToDictionary(i => i.ProductId);
        var unavailable = basket.Where(b => !items.ContainsKey(b.ProductId)).ToList();
        if (unavailable.Count > 0)
        {
            return Error.BusinessRule("ordering.product_unavailable", $"{unavailable.Count} product(s) are no longer sold. Update the cart.");
        }

        // 2. Exchange rates: everything is charged in MZN.
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

        var lines = basket.Select(b =>
        {
            var item = items[b.ProductId];
            var rate = rates[item.Price.Currency];
            return new OrderLine(item.ProductId, item.SellerId, item.Sku, item.Name, b.Quantity, rate.Convert(item.Price), rate.Convert(item.Price * b.Quantity));
        }).ToList();
        var subtotal = lines.Select(l => l.LineTotal).Aggregate(Money.Zero(Currency.Mzn), (a, b) => a + b);

        // 3. Promotions.
        var promotion = await promotions.BestPromotionAsync(new PromotionContext(
            subtotal, lines.Sum(l => l.Quantity), [.. lines.Select(l => items[l.ProductId].Category).Distinct()],
            deliveryCity, customer.Type, paymentMethod), cancellationToken);

        // 4. Shipping: one parcel per seller, leaving from the seller's city (Strategy inside the Shipping module).
        var sellerIds = lines.Select(l => l.SellerId).Distinct().ToList();
        var sellers = (await catalog.GetSellersAsync(sellerIds, cancellationToken)).ToDictionary(s => s.SellerId);
        var parcels = sellerIds
            .Where(sellers.ContainsKey)
            .Select(id => new OrderParcel(id, sellers[id].City, basket.Where(b => items[b.ProductId].SellerId == id).Sum(b => items[b.ProductId].WeightKg * b.Quantity)))
            .ToList();
        var quote = await shipping.QuoteAsync(new ShippingQuoteRequest(deliveryCity, delivery.District, delivery.Method, delivery.PickupPointId,
            [.. parcels.Select(p => new ParcelRequest(p.SellerId, p.OriginCity, p.WeightKg))]), cancellationToken);
        if (quote.IsFailure)
        {
            return quote.Error;
        }

        var totals = new OrderTotals(subtotal, promotion.Name, promotion.Discount, quote.Value.Fee);
        var orderDelivery = new OrderDelivery(quote.Value.Method, delivery.District, quote.Value.PickupPointId, quote.Value.EstimatedDelivery);

        // 5. Acceptance chain (NUIT, stock, delivery zone, anti-fraud, seller minimum).
        var accepted = await acceptance.AcceptAsync(new OrderAcceptanceContext(
            customer, deliveryCity, paymentMethod,
            [.. lines.Select(l => new AcceptanceLine(l.ProductId, l.SellerId, l.Sku, l.Quantity, items[l.ProductId].AvailableStock, l.LineTotal))],
            sellers, totals.Total), cancellationToken);
        if (accepted.IsFailure)
        {
            return accepted.Error;
        }

        // 6. The order.
        var now = timeProvider.GetUtcNow();
        var order = Order.Place(OrderId.New(), await numbers.NextAsync(cancellationToken), source, sourceId, customer, deliveryCity, orderDelivery, parcels,
            paymentMethod, lines, totals, now);
        if (order.IsFailure)
        {
            return order.Error;
        }

        // 7. Payment: mobile money and card wait for the customer's confirmation; cash is paid to the courier.
        if (!order.Value.PaysOnDelivery)
        {
            order.Value.RequestPayment(now);
        }

        orders.Add(order.Value);
        return ToResult(order.Value, alreadyPlaced: false);
    }

    private static CheckoutResult ToResult(Order order, bool alreadyPlaced)
    {
        var totals = order.Totals;
        var nextStep = order.PaysOnDelivery
            ? $"Pay {totals.Total} to the courier on delivery."
            : $"Confirm the {order.PaymentMethod} payment of {totals.Total} on {order.Customer.Phone}.";

        return new CheckoutResult(order.Id.Value, order.Number, order.DisplayNumber, order.Status.ToString(),
            totals.Subtotal.Amount, totals.PromotionName, totals.Discount.Amount, totals.ShippingFee.Amount, totals.Total.Amount,
            order.Delivery.Method, order.Delivery.EstimatedDelivery, nextStep, alreadyPlaced);
    }
}
