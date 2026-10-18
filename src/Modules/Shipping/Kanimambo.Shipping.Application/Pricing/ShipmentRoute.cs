namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>One parcel's trip: from the seller's city to the customer (or a pickup point).</summary>
public sealed record ShipmentRoute(string OriginCity, string DestinationCity, string? District, string? PickupPointId, decimal WeightKg);
