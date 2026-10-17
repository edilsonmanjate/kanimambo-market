using System.Globalization;
using System.Text.RegularExpressions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

/// <summary>
/// Business customers invoice with their NUIT (9 digits). Only the format is checked here; the official
/// check-digit rule is not published in a form we can rely on, so it is deliberately not invented.
/// </summary>
public sealed partial class BusinessCustomersNeedValidNuit : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var nuit = context.Customer.Nuit;

        if (context.Customer.IsBusiness && nuit.Length == 0)
        {
            return Task.FromResult(Result.Failure(Error.Validation(new FieldError("nuit", "Business orders need the company NUIT."))));
        }

        return Task.FromResult(nuit.Length == 0 || NuitPattern().IsMatch(nuit)
            ? Result.Success()
            : Result.Failure(Error.Validation(new FieldError("nuit", "A NUIT has exactly 9 digits."))));
    }

    [GeneratedRegex(@"^\d{9}$")]
    private static partial Regex NuitPattern();
}

public sealed class StockMustBeAvailable : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var shortLines = context.Lines.Where(l => l.Quantity > l.AvailableStock).Select(l => $"{l.Sku} ({l.AvailableStock} left)").ToList();
        return Task.FromResult(shortLines.Count == 0
            ? Result.Success()
            : Result.Failure(Error.BusinessRule("ordering.insufficient_stock", $"Not enough stock: {string.Join(", ", shortLines)}.")));
    }
}

public sealed class SellersMustDeliverToTheCity : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var notCovered = context.Lines
            .Select(l => l.SellerId)
            .Distinct()
            .Select(id => context.Sellers.GetValueOrDefault(id))
            .Where(s => s is null || !s.IsActive || !s.DeliveryCities.Contains(context.DeliveryCity, StringComparer.OrdinalIgnoreCase))
            .Select(s => s?.Name ?? "unknown seller")
            .ToList();

        return Task.FromResult(notCovered.Count == 0
            ? Result.Success()
            : Result.Failure(Error.BusinessRule("ordering.delivery_zone_not_covered",
                $"{string.Join(", ", notCovered)} do(es) not deliver to {context.DeliveryCity}.")));
    }
}

/// <summary>Caps how much one phone number can order in 24 hours (a common pattern in mobile-money fraud).</summary>
public sealed class PhoneMustBeWithinFraudLimits(IOrderRepository orders, AntiFraudSettings settings, TimeProvider timeProvider) : OrderAcceptanceHandler
{
    protected override async Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (count, total) = await orders.RecentByPhoneAsync(context.Customer.Phone, timeProvider.GetUtcNow().AddDays(-1), cancellationToken);

        if (count >= settings.MaxOrdersPerPhonePerDay)
        {
            return Error.BusinessRule("ordering.fraud_limit_orders", $"Too many orders from {Mask(context.Customer.Phone)} today; contact support.");
        }

        return total + context.Total.Amount > settings.MaxValuePerPhonePerDayMzn
            ? Error.BusinessRule("ordering.fraud_limit_value", string.Create(CultureInfo.InvariantCulture,
                $"Orders from {Mask(context.Customer.Phone)} would exceed {settings.MaxValuePerPhonePerDayMzn:0.00} MZN today; contact support."))
            : Result.Success();
    }

    private static string Mask(string phone) => phone.Length > 4 ? $"…{phone[^4..]}" : phone;
}

/// <summary>Wholesalers set a minimum order value; it applies per seller within a multi-seller order.</summary>
public sealed class SellerMinimumsMustBeMet : OrderAcceptanceHandler
{
    protected override Task<Result> CheckAsync(OrderAcceptanceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var belowMinimum = context.Lines
            .GroupBy(l => l.SellerId)
            .Select(g => (Seller: context.Sellers[g.Key], Total: g.Select(l => l.LineTotal).Aggregate(Money.Zero(Currency.Mzn), (a, b) => a + b)))
            .Where(x => x.Total < x.Seller.MinimumOrder)
            .Select(x => $"{x.Seller.Name} (minimum {x.Seller.MinimumOrder}, have {x.Total})")
            .ToList();

        return Task.FromResult(belowMinimum.Count == 0
            ? Result.Success()
            : Result.Failure(Error.BusinessRule("ordering.below_seller_minimum", $"Below the seller minimum: {string.Join("; ", belowMinimum)}.")));
    }
}
