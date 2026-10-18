using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.Courier;

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
