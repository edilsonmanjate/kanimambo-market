using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>
/// Strategy: one way of delivering and pricing a parcel. Strategies are registered in DI and found by
/// <see cref="Method"/>; adding a delivery method means adding a class and its configuration — no switch to edit.
/// A strategy that cannot serve a route (out of area, too heavy) says so with a typed error.
/// </summary>
public interface IShippingStrategy
{
    string Method { get; }

    string Description { get; }

    Result<ShippingLeg> Quote(ShipmentRoute route);
}
