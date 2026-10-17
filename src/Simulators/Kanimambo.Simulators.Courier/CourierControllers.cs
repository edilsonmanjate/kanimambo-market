using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.Courier;

// Two deliberately different, simulated courier APIs. The couriers are fictional and the formats are the
// simulator's own contracts; the API's adapters translate each of them into one shared interface.

public sealed record TxovaPlace(string City, string? District, string? Address, string? Phone);

public sealed record TxovaDeliveryRequest(string Reference, TxovaPlace Pickup, TxovaPlace Dropoff, decimal WeightKg, string Vehicle, decimal CashToCollect, Uri WebhookUrl);

/// <summary>"Txova Express" (fictional, simulated): same-day riders; English JSON, kg, event words, 201/422 answers.</summary>
[ApiController]
[Route("txova/v1/deliveries")]
public sealed class TxovaController(Waybills waybills, WebhookDispatcher webhooks, SimulatorOptions options) : ControllerBase
{
    private static readonly string[] Area = ["Maputo", "Matola"];

    [HttpPost]
    public IActionResult Create(TxovaDeliveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scenario = Waybills.ScenarioFor(request.Dropoff.Phone ?? string.Empty);
        if (waybills.SimulateOutage(request.Reference, scenario))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (scenario == Scenario.Refused || !Area.Contains(request.Dropoff.City, StringComparer.OrdinalIgnoreCase))
        {
            return UnprocessableEntity(new { error = "OUT_OF_AREA", message = $"Txova does not deliver to {request.Dropoff.City}." });
        }

        if (request.WeightKg > 150)
        {
            return UnprocessableEntity(new { error = "TOO_HEAVY", message = "Max 150 kg per delivery." });
        }

        var (waybill, created) = waybills.Open(request.Reference, () => new Waybill
        {
            Courier = "Txova",
            Reference = request.Reference,
            TrackingCode = $"TX-{Random.Shared.Next(100_000, 999_999)}",
            WebhookUrl = request.WebhookUrl,
        });

        if (created)
        {
            var delivered = scenario == Scenario.RecipientAbsent
                ? Event(waybill, "FAILED", "RECIPIENT_ABSENT")
                : Event(waybill, "DELIVERED", null);
            webhooks.Schedule(waybill, [(options.PickupDelayMilliseconds, Event(waybill, "PICKED_UP", null)), (options.DeliveryDelayMilliseconds, delivered)]);
        }

        return StatusCode(created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
            new { deliveryId = waybill.TrackingCode, reference = waybill.Reference, status = "ASSIGNED" });
    }

    private static string Event(Waybill waybill, string name, string? reason) =>
        JsonSerializer.Serialize(new { deliveryId = waybill.TrackingCode, reference = waybill.Reference, @event = name, reason }, JsonSerializerOptions.Web);
}

public sealed record MbilaGuiaRequest(
    string ReferenciaCliente,
    string Origem,
    string Destino,
    string? Morada,
    string TelefoneDestinatario,
    int PesoGramas,
    string ValorCobranca,
    Uri UrlNotificacao);

/// <summary>"Mbila Cargo" (fictional, simulated): inter-provincial cargo; Portuguese fields, grams, <c>codigo</c> answers.</summary>
[ApiController]
[Route("mbila/api/guias")]
public sealed class MbilaController(Waybills waybills, WebhookDispatcher webhooks, SimulatorOptions options) : ControllerBase
{
    private static readonly string[] Served = ["Maputo", "Matola", "Beira", "Nampula", "Quelimane", "Tete", "Inhambane", "Xai-Xai", "Chimoio", "Pemba", "Lichinga"];

    [HttpPost]
    public IActionResult Create(MbilaGuiaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scenario = Waybills.ScenarioFor(request.TelefoneDestinatario);
        if (waybills.SimulateOutage(request.ReferenciaCliente, scenario))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (scenario == Scenario.Refused || !Served.Contains(request.Destino, StringComparer.OrdinalIgnoreCase))
        {
            return Ok(new { codigo = "E12", mensagem = $"Destino não servido: {request.Destino}", numeroGuia = (string?)null });
        }

        if (request.PesoGramas > 500_000)
        {
            return Ok(new { codigo = "E30", mensagem = "Peso excede o limite de 500 kg", numeroGuia = (string?)null });
        }

        var (waybill, created) = waybills.Open(request.ReferenciaCliente, () => new Waybill
        {
            Courier = "Mbila",
            Reference = request.ReferenciaCliente,
            TrackingCode = $"MB{Random.Shared.Next(10_000_000, 99_999_999)}",
            WebhookUrl = request.UrlNotificacao,
        });

        if (created)
        {
            var last = scenario == Scenario.RecipientAbsent ? Estado(waybill, "DEVOLVIDO", "Destinatário ausente") : Estado(waybill, "ENTREGUE", null);
            webhooks.Schedule(waybill,
            [
                (options.PickupDelayMilliseconds, Estado(waybill, "RECOLHIDO", null)),
                (options.TransitDelayMilliseconds, Estado(waybill, "EM_TRANSITO", $"{request.Origem} → {request.Destino}")),
                (options.DeliveryDelayMilliseconds, last),
            ]);
        }

        return Ok(new { codigo = "00", mensagem = "Guia emitida", numeroGuia = waybill.TrackingCode });
    }

    private static string Estado(Waybill waybill, string estado, string? observacao) =>
        JsonSerializer.Serialize(new { numeroGuia = waybill.TrackingCode, referenciaCliente = waybill.Reference, estado, observacao });
}
