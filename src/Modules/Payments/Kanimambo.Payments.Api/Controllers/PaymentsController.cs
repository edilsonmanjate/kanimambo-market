using Kanimambo.Payments.Application;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Payments.Api.Controllers;

[Route("api/payments")]
public sealed class PaymentsController(IMediator mediator) : ApiControllerBase
{
    public const string SignatureHeader = "X-Signature";

    /// <summary>
    /// Asynchronous answers from the operators (the customer typed the PIN, or the payment failed). Anonymous,
    /// but every body must carry the operator's HMAC signature; the adapter verifies and translates it.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("callbacks/{operatorName}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Callback(string operatorName, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        return NoContentOrProblem(await mediator.Send(
            new HandleGatewayCallback(operatorName, body, Request.Headers[SignatureHeader].FirstOrDefault()), cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.Backoffice)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetPayment(id), cancellationToken));

    /// <summary>Every payment attempt of an order, with its lifecycle and the operator's native codes.</summary>
    [Authorize(Policy = AuthorizationPolicies.Backoffice)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForOrder([FromQuery] Guid orderId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListPaymentsForOrder(orderId), cancellationToken));
}
