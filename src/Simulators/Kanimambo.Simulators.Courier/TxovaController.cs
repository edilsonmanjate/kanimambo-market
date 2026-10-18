using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.Courier;

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
