using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Ordering.Api.Controllers;

/// <summary>Checkout input. Delivery is optional: <c>deliveryMethod</c> is SameDay, Courier or Pickup (see GET /api/shipping/options).</summary>
public sealed record CheckoutRequest(Guid CartId, PaymentMethod PaymentMethod, string Phone, string? Nuit, string? DeliveryMethod = null, string? DeliveryDistrict = null, string? PickupPointId = null);
