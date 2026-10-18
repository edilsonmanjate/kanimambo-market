using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Domain;

/// <summary>What one parcel needs to be shipped, decided when the order is dispatched.</summary>
public sealed record ParcelPlan(Guid SellerId, string OriginCity, decimal WeightKg, string Courier, string? Vehicle, Money CashToCollect);
