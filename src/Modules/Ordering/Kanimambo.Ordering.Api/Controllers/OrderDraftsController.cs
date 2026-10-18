using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Idempotency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Ordering.Api.Controllers;

[Authorize]
[Route("api/order-drafts")]
public sealed class OrderDraftsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<DraftOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetDraftOrder(id), cancellationToken));

    [HttpPut("{id:guid}/lines/{productId:guid}")]
    [ProducesResponseType<DraftOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetLine(Guid id, Guid productId, DraftLineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new SetDraftLine(id, productId, request.Quantity), cancellationToken));
    }

    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType<DraftOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Undo(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new UndoDraftChange(id), cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [RequiresIdempotencyKey]
    [ProducesResponseType<CheckoutResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(Guid id, SubmitDraftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new SubmitDraftOrder(id, request.PaymentMethod, request.Phone, request.Nuit,
            new DeliveryChoice(request.DeliveryMethod, request.DeliveryDistrict, request.PickupPointId)), cancellationToken));
    }
}
