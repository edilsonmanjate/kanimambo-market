using Kanimambo.Operations.Application;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Operations;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Operations.Api.Controllers;

/// <summary>For the people who run the system: outbox health and dead letters across modules (back-office).</summary>
[Authorize(Policy = AuthorizationPolicies.Backoffice)]
[Route("api/operations/outbox")]
public sealed class OperationsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OutboxSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetOutboxOverview(), cancellationToken));

    [HttpGet("dead-letters")]
    [ProducesResponseType<IReadOnlyList<DeadLetter>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeadLetters([FromQuery] string? module, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListDeadLetters(module), cancellationToken));

    /// <summary>Replay a dead letter once its cause is fixed; only the subscribers that never handled it are called.</summary>
    [HttpPost("{module}/dead-letters/{id:guid}/replay")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Replay(string module, Guid id, CancellationToken cancellationToken) =>
        NoContentOrProblem(await mediator.Send(new ReplayDeadLetter(module, id), cancellationToken));
}
