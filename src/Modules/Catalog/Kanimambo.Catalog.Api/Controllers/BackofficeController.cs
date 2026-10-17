using Kanimambo.Catalog.Application.Backoffice;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Catalog.Api.Controllers;

public sealed record PriceAdjustmentRequest(decimal Percentage, string Reason);

public sealed record SuspensionRequest(string Reason);

/// <summary>Audited, undoable back-office operations. Requires a token with the <c>backoffice</c> role.</summary>
[Route("api/catalog/backoffice")]
[Authorize(Policy = AuthorizationPolicies.Backoffice)]
public sealed class BackofficeController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("sellers/{sellerId:guid}/price-adjustments")]
    [ProducesResponseType<BackofficeOperationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjustPrices(Guid sellerId, PriceAdjustmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new AdjustSellerPrices(sellerId, request.Percentage, request.Reason), cancellationToken));
    }

    [HttpPost("sellers/{sellerId:guid}/suspensions")]
    [ProducesResponseType<BackofficeOperationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Suspend(Guid sellerId, SuspensionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new SuspendSeller(sellerId, request.Reason), cancellationToken));
    }

    [HttpPost("operations/{operationId:guid}/undo")]
    [ProducesResponseType<BackofficeOperationDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Undo(Guid operationId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new UndoBackofficeOperation(operationId), cancellationToken));

    /// <summary>The audit log: who did what and when, newest first.</summary>
    [HttpGet("operations")]
    [ProducesResponseType<PagedResult<BackofficeOperationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        OkOrProblem(await mediator.Send(new ListBackofficeOperations(page, pageSize), cancellationToken));
}
