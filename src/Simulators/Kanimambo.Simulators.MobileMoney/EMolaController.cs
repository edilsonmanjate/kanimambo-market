using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.MobileMoney;

/// <summary>"e-Mola" (simulated): <c>status</c> words, textual error codes, amounts in cents.</summary>
[ApiController]
[Route("emola/payments")]
public sealed class EMolaController(Ledger ledger, CallbackDispatcher callbacks) : ControllerBase
{
    [HttpPost]
    public IActionResult Charge(EMolaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scenario = Ledger.ScenarioFor(request.Msisdn);
        if (ledger.SimulateOutage(request.Reference, scenario))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (scenario is Scenario.Duplicate or Scenario.InvalidNumber)
        {
            return Ok(Body(request.Reference, "REJECTED", scenario == Scenario.Duplicate ? "DUPLICATE_REFERENCE" : "INVALID_MSISDN", null));
        }

        var transaction = new SimulatedTransaction
        {
            Reference = request.Reference,
            Operator = "EMola",
            Phone = request.Msisdn,
            Amount = request.AmountCents / 100m,
            CallbackUrl = request.CallbackUrl,
            TransactionId = $"EM-{Guid.NewGuid():N}"[..15],
        };
        if (!ledger.TryOpen(transaction))
        {
            return Ok(Body(request.Reference, "REJECTED", "DUPLICATE_REFERENCE", null));
        }

        callbacks.Schedule(transaction, scenario, t => JsonSerializer.Serialize(Body(t.Reference,
            t.State == TransactionState.Completed ? "SUCCESS" : "FAILED", t.State == TransactionState.Failed ? "BALANCE_LOW" : null, t.TransactionId),
            JsonSerializerOptions.Web));

        return Ok(Body(request.Reference, "PENDING", null, transaction.TransactionId));
    }

    [HttpGet("{reference:guid}")]
    public IActionResult Status(Guid reference)
    {
        var t = ledger.Find(reference);
        return t is null
            ? NotFound()
            : Ok(Body(reference, t.State switch { TransactionState.Completed => "SUCCESS", TransactionState.Failed => "FAILED", _ => "PENDING" },
                t.State == TransactionState.Failed ? "BALANCE_LOW" : null, t.TransactionId));
    }

    private static object Body(Guid reference, string status, string? errorCode, string? transactionId) =>
        new { reference, status, errorCode, transactionId };
}
