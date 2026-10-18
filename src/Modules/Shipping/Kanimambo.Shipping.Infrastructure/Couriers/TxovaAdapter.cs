using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Couriers;
using Kanimambo.Shipping.Domain;

namespace Kanimambo.Shipping.Infrastructure.Couriers;

/// <summary>Adapter for the simulated "Txova Express" API: same-day riders, English JSON, kg, 201 / 422 answers, event words.</summary>
internal sealed class TxovaAdapter(HttpClient http, CourierConnectionOptions options) : ICourierGateway
{
    public const string CourierName = "Txova";

    private static readonly Dictionary<string, CourierFailureReason> Errors = new(StringComparer.Ordinal)
    {
        ["OUT_OF_AREA"] = CourierFailureReason.DestinationNotServed,
        ["TOO_HEAVY"] = CourierFailureReason.Overweight,
    };

    private static readonly Dictionary<string, ParcelStatus> Events = new(StringComparer.Ordinal)
    {
        ["PICKED_UP"] = ParcelStatus.PickedUp,
        ["DELIVERED"] = ParcelStatus.Delivered,
        ["FAILED"] = ParcelStatus.Failed,
    };

    public string Name => CourierName;

    public async Task<Result<CourierBooking>> BookAsync(CourierBookingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new TxovaRequest(
            request.Reference,
            new TxovaPlace(request.OriginCity, null, null, null),
            new TxovaPlace(request.DestinationCity, request.District, request.DropoffAddress, request.RecipientPhone),
            request.WeightKg,
            string.Equals(request.Vehicle, "Txopela", StringComparison.OrdinalIgnoreCase) ? "TXOPELA" : "MOTO",
            request.CashToCollect.Amount,
            request.WebhookUrl.ToString());

        var sent = await CourierHttp.SendAsync(() => http.PostAsJsonAsync("txova/v1/deliveries", body, CourierHttp.Json, cancellationToken), cancellationToken);
        if (sent.IsFailure)
        {
            return sent.Error;
        }

        using var response = sent.Value;
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var refusal = await response.Content.ReadFromJsonAsync<TxovaError>(CourierHttp.Json, cancellationToken);
            var code = refusal?.Error ?? "UNKNOWN";
            return new CourierError(Errors.GetValueOrDefault(code, CourierFailureReason.Rejected), code, $"Txova: {refusal?.Message ?? code}");
        }

        if (!response.IsSuccessStatusCode)
        {
            return new CourierError(CourierFailureReason.Rejected, ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), "Txova refused the delivery.");
        }

        var accepted = await response.Content.ReadFromJsonAsync<TxovaDelivery>(CourierHttp.Json, cancellationToken);
        return accepted is null ? CourierHttp.Unavailable("Txova returned an empty response.") : new CourierBooking(accepted.DeliveryId);
    }

    public Result<CourierUpdate> ParseWebhook(string body, string? signature)
    {
        var webhook = CourierHttp.ParseSigned<TxovaEvent>(body, signature, options.WebhookSecret);
        if (webhook.IsFailure)
        {
            return webhook.Error;
        }

        return Events.TryGetValue(webhook.Value.Event, out var status)
            ? new CourierUpdate(webhook.Value.Reference, status, webhook.Value.Reason is null ? null : $"Txova: {webhook.Value.Reason}")
            : Error.Validation(new FieldError("event", $"Unknown Txova event '{webhook.Value.Event}'."));
    }

    private sealed record TxovaPlace(string City, string? District, string? Address, string? Phone);

    private sealed record TxovaRequest(string Reference, TxovaPlace Pickup, TxovaPlace Dropoff, decimal WeightKg, string Vehicle, decimal CashToCollect, string WebhookUrl);

    private sealed record TxovaDelivery(string DeliveryId, string Reference, string Status);

    private sealed record TxovaError(string Error, string? Message);

    private sealed record TxovaEvent(string DeliveryId, string Reference, string Event, string? Reason);
}
