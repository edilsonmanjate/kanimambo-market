using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Domain;

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

public sealed record CourierBooking(string TrackingCode);

/// <summary>A tracking update from a courier, in our own vocabulary.</summary>
public sealed record CourierUpdate(string Reference, ParcelStatus Status, string? Detail);

public enum CourierFailureReason
{
    DestinationNotServed,
    Overweight,
    InvalidRecipient,
    Rejected,
    Unavailable,
}

/// <summary>A courier error translated into the shared vocabulary, keeping the courier's native code for support.</summary>
public sealed record CourierError : Error
{
    public CourierError(CourierFailureReason reason, string? nativeCode, string message)
        : base($"shipping.courier_{reason.ToString().ToLowerInvariant()}", message, reason == CourierFailureReason.Unavailable ? ErrorType.Unavailable : ErrorType.BusinessRule)
    {
        Reason = reason;
        NativeCode = nativeCode;
    }

    public CourierFailureReason Reason { get; }

    public string? NativeCode { get; }

    /// <summary>Outages are retried (by the outbox); refusals fail the parcel.</summary>
    public bool IsTransient => Reason == CourierFailureReason.Unavailable;
}

/// <summary>
/// Adapter target for couriers: each courier has its own API (fields, units, status words, signatures); one adapter
/// per courier translates it into this interface.
/// </summary>
public interface ICourierGateway
{
    string Name { get; }

    Task<Result<CourierBooking>> BookAsync(CourierBookingRequest request, CancellationToken cancellationToken);

    /// <summary>Verifies the courier's signature and translates its tracking webhook.</summary>
    Result<CourierUpdate> ParseWebhook(string body, string? signature);
}

public interface ICourierGatewayProvider
{
    Result<ICourierGateway> For(string courier);
}
