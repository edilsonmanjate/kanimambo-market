using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Contracts;

/// <summary>A priced delivery option. <see cref="PickupPointId"/> is set when the parcels go to a pickup point.</summary>
public sealed record ShippingQuote(string Method, string Description, Money Fee, DateOnly EstimatedDelivery, string? PickupPointId, IReadOnlyList<ParcelQuote> Parcels);
