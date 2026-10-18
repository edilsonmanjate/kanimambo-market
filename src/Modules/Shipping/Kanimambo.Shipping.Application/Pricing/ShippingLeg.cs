using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>How a strategy would carry a parcel, and for how much.</summary>
public sealed record ShippingLeg(Money Fee, string Courier, string? Vehicle, DateOnly EstimatedDelivery, string? PickupPointId = null);
