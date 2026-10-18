using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Application.Couriers;

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
