using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Application.Pricing;

internal static class ShippingErrors
{
    public static Error NotServed(string method, string reason) =>
        Error.BusinessRule("shipping.not_served", $"{method} delivery is not available: {reason}");
}
