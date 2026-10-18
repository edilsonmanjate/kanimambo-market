using Kanimambo.Shipping.Application.Pricing;
using Kanimambo.Shipping.Infrastructure.Couriers;

namespace Kanimambo.Shipping.Infrastructure;

/// <summary>Connection settings, next to the pricing settings of <see cref="ShippingOptions"/> in the same section.</summary>
public sealed class ShippingConnectionOptions
{
    /// <summary>Public address of this API, used to build the webhook URLs given to couriers.</summary>
    public Uri WebhookBaseAddress { get; set; } = new("http://localhost:5080/");

    public Dictionary<string, CourierConnectionOptions> Couriers { get; } = new(StringComparer.OrdinalIgnoreCase);
}
