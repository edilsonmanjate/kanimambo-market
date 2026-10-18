using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Application.Couriers;

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
