namespace Kanimambo.Shipping.Contracts;

/// <summary>One seller's part of an order: it leaves from the seller's city and weighs what its products weigh.</summary>
public sealed record ParcelRequest(Guid SellerId, string OriginCity, decimal WeightKg);
