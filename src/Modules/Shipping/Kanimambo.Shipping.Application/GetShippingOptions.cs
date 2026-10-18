using Kanimambo.SharedKernel.Messaging;

namespace Kanimambo.Shipping.Application;

/// <summary>Delivery options for a cart before checkout, cheapest first.</summary>
public sealed record GetShippingOptions(Guid CartId, string? District, string? PickupPointId) : IQuery<IReadOnlyList<ShippingOptionDto>>;
