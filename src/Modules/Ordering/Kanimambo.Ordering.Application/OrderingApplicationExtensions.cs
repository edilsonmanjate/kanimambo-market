using Kanimambo.Ordering.Application.Acceptance;
using Kanimambo.Ordering.Application.Checkout;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Ordering.Application;

public static class OrderingApplicationExtensions
{
    /// <summary>
    /// Registers the checkout facade and the acceptance chain. The chain order is declared here; pass
    /// <paramref name="acceptanceChain"/> to compose a different order or a different set of checks.
    /// </summary>
    public static IServiceCollection AddOrderingApplication(
        this IServiceCollection services,
        Action<OrderAcceptanceChainBuilder>? acceptanceChain = null)
    {
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddOrderAcceptanceChain(acceptanceChain ?? (chain => chain
            .Then<BusinessCustomersNeedValidNuit>()
            .Then<StockMustBeAvailable>()
            .Then<SellersMustDeliverToTheCity>()
            .Then<PhoneMustBeWithinFraudLimits>()
            .Then<SellerMinimumsMustBeMet>()));
        return services;
    }
}
