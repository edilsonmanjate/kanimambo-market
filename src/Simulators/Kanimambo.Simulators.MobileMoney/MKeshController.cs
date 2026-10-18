using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.MobileMoney;

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
