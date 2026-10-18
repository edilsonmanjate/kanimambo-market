namespace Kanimambo.Ordering.Domain;

/// <summary>One seller's share of the order, as it will travel: from the seller's city, with this weight.</summary>
public sealed record OrderParcel(Guid SellerId, string OriginCity, decimal WeightKg);
