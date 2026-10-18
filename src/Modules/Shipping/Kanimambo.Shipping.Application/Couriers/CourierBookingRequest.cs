using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Application.Couriers;

/// <summary>
/// What we ask a courier: collect this parcel at the seller and bring it to the customer (or a pickup point).
/// <see cref="Reference"/> is ours and deterministic, so booking the same parcel twice is recognised by the courier.
/// </summary>
public sealed record CourierBookingRequest(
    string Reference,
    string OriginCity,
    string DestinationCity,
    string? District,
    string? DropoffAddress,
    string RecipientPhone,
    decimal WeightKg,
    string? Vehicle,
    Money CashToCollect,
    Uri WebhookUrl);
