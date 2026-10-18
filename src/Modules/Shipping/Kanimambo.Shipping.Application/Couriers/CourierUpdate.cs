using Kanimambo.Shipping.Domain;

namespace Kanimambo.Shipping.Application.Couriers;

/// <summary>A tracking update from a courier, in our own vocabulary.</summary>
public sealed record CourierUpdate(string Reference, ParcelStatus Status, string? Detail);
