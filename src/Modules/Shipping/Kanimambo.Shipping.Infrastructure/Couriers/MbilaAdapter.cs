using System.Globalization;
using System.Net.Http.Json;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Couriers;
using Kanimambo.Shipping.Domain;

namespace Kanimambo.Shipping.Infrastructure.Couriers;

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
