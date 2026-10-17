using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.MobileMoney;

// Three deliberately different, simulated wire formats. They are the simulator's own contracts — not the
// operators' official APIs — and the API's adapters translate each of them into one shared interface.

public sealed record MPesaRequest(
    [property: JsonPropertyName("input_TransactionReference")] string TransactionReference,
    [property: JsonPropertyName("input_CustomerMSISDN")] string CustomerMsisdn,
    [property: JsonPropertyName("input_Amount")] string Amount,
    [property: JsonPropertyName("input_ThirdPartyReference")] Guid ThirdPartyReference,
    [property: JsonPropertyName("input_CallbackUrl")] Uri CallbackUrl);

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

public sealed record EMolaRequest(Guid Reference, string Msisdn, long AmountCents, Uri CallbackUrl);

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

public sealed record MKeshRequest(Guid Ref, string Telefone, string Valor, Uri UrlRetorno);

/// <summary>"mKesh" (simulated): Portuguese fields, numeric <c>codigo</c>s, decimal comma amounts.</summary>
[ApiController]
[Route("mkesh/v1/cobrancas")]
public sealed class MKeshController(Ledger ledger, CallbackDispatcher callbacks) : ControllerBase
{
    [HttpPost]
    public IActionResult Charge(MKeshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scenario = Ledger.ScenarioFor(request.Telefone);
        if (ledger.SimulateOutage(request.Ref, scenario))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (scenario is Scenario.Duplicate or Scenario.InvalidNumber)
        {
            return Ok(new { codigo = scenario == Scenario.Duplicate ? 94 : 14, mensagem = scenario == Scenario.Duplicate ? "Transacção duplicada" : "Número inválido", idTransacao = (string?)null });
        }

        var transaction = new SimulatedTransaction
        {
            Reference = request.Ref,
            Operator = "MKesh",
            Phone = request.Telefone,
            Amount = decimal.Parse(request.Valor.Replace(',', '.'), CultureInfo.InvariantCulture),
            CallbackUrl = request.UrlRetorno,
            TransactionId = $"MK{Random.Shared.Next(100_000, 999_999)}",
        };
        if (!ledger.TryOpen(transaction))
        {
            return Ok(new { codigo = 94, mensagem = "Transacção duplicada", idTransacao = (string?)null });
        }

        callbacks.Schedule(transaction, scenario, t => JsonSerializer.Serialize(
            new { @ref = t.Reference, idTransacao = t.TransactionId, codigo = t.State == TransactionState.Completed ? 0 : 51 }));

        return Ok(new { codigo = 0, mensagem = "Cobrança aceite", idTransacao = transaction.TransactionId });
    }

    [HttpGet("{reference:guid}")]
    public IActionResult Status(Guid reference)
    {
        var t = ledger.Find(reference);
        return t is null
            ? NotFound()
            : Ok(new
            {
                estado = t.State switch { TransactionState.Completed => "CONCLUIDA", TransactionState.Failed => "FALHADA", _ => "PENDENTE" },
                codigo = t.State == TransactionState.Failed ? 51 : 0,
                idTransacao = t.TransactionId,
            });
    }
}
