using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.MobileMoney;

/// <summary>"M-Pesa" (simulated): <c>input_/output_</c> fields, <c>INS-*</c> codes.</summary>
[ApiController]
[Route("mpesa")]
public sealed class MPesaController(Ledger ledger, CallbackDispatcher callbacks) : ControllerBase
{
    [HttpPost("c2b")]
    public IActionResult Charge(MPesaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scenario = Ledger.ScenarioFor(request.CustomerMsisdn);
        if (ledger.SimulateOutage(request.ThirdPartyReference, scenario))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var immediate = scenario switch
        {
            Scenario.Duplicate => "INS-5",
            Scenario.InvalidNumber => "INS-10",
            _ => null,
        };
        if (immediate is not null)
        {
            return Ok(Reply(immediate, "Request rejected", null));
        }

        var transaction = new SimulatedTransaction
        {
            Reference = request.ThirdPartyReference,
            Operator = "MPesa",
            Phone = request.CustomerMsisdn,
            Amount = decimal.Parse(request.Amount, CultureInfo.InvariantCulture),
            CallbackUrl = request.CallbackUrl,
            TransactionId = $"MP{Random.Shared.Next(10_000_000, 99_999_999)}",
        };
        if (!ledger.TryOpen(transaction))
        {
            return Ok(Reply("INS-5", "Duplicate transaction", null));
        }

        callbacks.Schedule(transaction, scenario, t => JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["output_ResponseCode"] = t.State == TransactionState.Completed ? "INS-0" : "INS-6",
            ["output_TransactionID"] = t.TransactionId,
            ["output_ThirdPartyReference"] = t.Reference.ToString(),
        }));

        return Ok(Reply("INS-0", "Request processed successfully", transaction.TransactionId));
    }

    [HttpGet("status/{reference:guid}")]
    public IActionResult Status(Guid reference)
    {
        var transaction = ledger.Find(reference);
        return transaction is null
            ? NotFound()
            : Ok(new Dictionary<string, string?>
            {
                ["output_ResponseCode"] = transaction.State == TransactionState.Failed ? "INS-6" : "INS-0",
                ["output_ResponseTransactionStatus"] = transaction.State switch
                {
                    TransactionState.Completed => "Completed",
                    TransactionState.Failed => "Failed",
                    _ => "Pending",
                },
                ["output_TransactionID"] = transaction.TransactionId,
            });
    }

    private static Dictionary<string, string?> Reply(string code, string description, string? transactionId) => new()
    {
        ["output_ResponseCode"] = code,
        ["output_ResponseDesc"] = description,
        ["output_TransactionID"] = transactionId,
    };
}
