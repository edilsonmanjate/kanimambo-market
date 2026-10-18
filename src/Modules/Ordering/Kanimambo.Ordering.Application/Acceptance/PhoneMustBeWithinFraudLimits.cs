using System.Globalization;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Acceptance;

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
