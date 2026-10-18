using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Contracts;

/// <summary>How one parcel travels: its fee, the courier that carries it and, for same-day, the vehicle.</summary>
public sealed record ParcelQuote(Guid SellerId, Money Fee, string Courier, string? Vehicle, DateOnly EstimatedDelivery);
