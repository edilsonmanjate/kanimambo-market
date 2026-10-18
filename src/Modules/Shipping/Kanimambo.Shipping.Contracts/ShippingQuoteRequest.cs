namespace Kanimambo.Shipping.Contracts;

/// <summary>
/// What to deliver and where. <see cref="Method"/> is a shipping method name (<c>SameDay</c>, <c>Courier</c>,
/// <c>Pickup</c>…); when it is null the configured preference order picks the first method that can serve.
/// </summary>
public sealed record ShippingQuoteRequest(
    string DestinationCity,
    string? District,
    string? Method,
    string? PickupPointId,
    IReadOnlyList<ParcelRequest> Parcels);
