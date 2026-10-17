using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanimambo.SharedKernel.Infrastructure.Webhooks;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Couriers;
using Kanimambo.Shipping.Domain;

namespace Kanimambo.Shipping.Infrastructure.Couriers;

// The couriers below are fictional and their APIs are the ones implemented by the local simulator
// (Kanimambo.Simulators.Courier). They are deliberately different from each other to show what the adapters
// absorb. A real courier integration requires that courier's documentation and sandbox credentials.

public sealed class CourierConnectionOptions
{
    public Uri BaseAddress { get; set; } = new("https+http://courier");

    /// <summary>Shared secret for webhook signatures. Local development value only; real ones come from a vault.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}

internal static class CourierHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Sends the request; outages, 5xx and timeouts become a transient <see cref="CourierError"/>.</summary>
    public static async Task<Result<HttpResponseMessage>> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            var response = await send();
            if ((int)response.StatusCode >= 500)
            {
                response.Dispose();
                return Unavailable($"The courier answered {(int)response.StatusCode}.");
            }

            return response;
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable("The courier did not answer in time.");
        }
    }

    public static Result<T> ParseSigned<T>(string body, string? signature, string secret)
    {
        if (!WebhookSignature.IsValid(body, signature, secret))
        {
            return Error.Unauthorized("shipping.invalid_signature", "The webhook signature is not valid.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(body, Json);
            return parsed is null ? Error.Validation(new FieldError("body", "Empty webhook.")) : parsed;
        }
        catch (JsonException exception)
        {
            return Error.Validation(new FieldError("body", $"Unreadable webhook: {exception.Message}"));
        }
    }

    public static CourierError Unavailable(string message) => new(CourierFailureReason.Unavailable, null, message);
}

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

/// <summary>Adapter for the simulated "Mbila Cargo" API: Portuguese fields, grams, decimal-comma amounts, <c>codigo</c>s.</summary>
internal sealed class MbilaAdapter(HttpClient http, CourierConnectionOptions options) : ICourierGateway
{
    public const string CourierName = "Mbila";

    private static readonly Dictionary<string, CourierFailureReason> Codes = new(StringComparer.Ordinal)
    {
        ["E12"] = CourierFailureReason.DestinationNotServed,
        ["E30"] = CourierFailureReason.Overweight,
        ["E41"] = CourierFailureReason.InvalidRecipient,
    };

    private static readonly Dictionary<string, ParcelStatus> States = new(StringComparer.Ordinal)
    {
        ["RECOLHIDO"] = ParcelStatus.PickedUp,
        ["EM_TRANSITO"] = ParcelStatus.InTransit,
        ["ENTREGUE"] = ParcelStatus.Delivered,
        ["DEVOLVIDO"] = ParcelStatus.Failed,
    };

    public string Name => CourierName;

    public async Task<Result<CourierBooking>> BookAsync(CourierBookingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new MbilaGuia(
            request.Reference,
            request.OriginCity,
            request.DestinationCity,
            request.DropoffAddress ?? request.District,
            request.RecipientPhone,
            (int)Math.Ceiling(request.WeightKg * 1000m),
            request.CashToCollect.Amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ','),
            request.WebhookUrl.ToString());

        var sent = await CourierHttp.SendAsync(() => http.PostAsJsonAsync("mbila/api/guias", body, CourierHttp.Json, cancellationToken), cancellationToken);
        if (sent.IsFailure)
        {
            return sent.Error;
        }

        using var response = sent.Value;
        var answer = await response.Content.ReadFromJsonAsync<MbilaResposta>(CourierHttp.Json, cancellationToken);
        if (answer is null)
        {
            return CourierHttp.Unavailable("Mbila returned an empty response.");
        }

        return answer is { Codigo: "00", NumeroGuia: { } guia }
            ? new CourierBooking(guia)
            : new CourierError(Codes.GetValueOrDefault(answer.Codigo, CourierFailureReason.Rejected), answer.Codigo, $"Mbila: {answer.Mensagem}");
    }

    public Result<CourierUpdate> ParseWebhook(string body, string? signature)
    {
        var webhook = CourierHttp.ParseSigned<MbilaNotificacao>(body, signature, options.WebhookSecret);
        if (webhook.IsFailure)
        {
            return webhook.Error;
        }

        return States.TryGetValue(webhook.Value.Estado, out var status)
            ? new CourierUpdate(webhook.Value.ReferenciaCliente, status, webhook.Value.Observacao is null ? null : $"Mbila: {webhook.Value.Observacao}")
            : Error.Validation(new FieldError("estado", $"Unknown Mbila state '{webhook.Value.Estado}'."));
    }

    private sealed record MbilaGuia(
        string ReferenciaCliente,
        string Origem,
        string Destino,
        string? Morada,
        string TelefoneDestinatario,
        int PesoGramas,
        string ValorCobranca,
        string UrlNotificacao);

    private sealed record MbilaResposta(string Codigo, string? Mensagem, string? NumeroGuia);

    private sealed record MbilaNotificacao(string NumeroGuia, string ReferenciaCliente, string Estado, string? Observacao);
}
