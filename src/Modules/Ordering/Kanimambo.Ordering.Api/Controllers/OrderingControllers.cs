using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Idempotency;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Ordering.Api.Controllers;

public sealed record CheckoutRequest(Guid CartId, PaymentMethod PaymentMethod, string Phone, string? Nuit);

public sealed record SubmitDraftRequest(PaymentMethod PaymentMethod, string Phone, string? Nuit);

public sealed record StatusChangeRequest(string? Note);

public sealed record DraftLineRequest(int Quantity);

/// <summary>
/// Checkout is one call to the facade. A retry over a flaky connection must never create a second order,
/// so the Idempotency-Key header is required here (and the cart itself can only become one order).
/// </summary>
[Route("api/checkout")]
public sealed class CheckoutController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [RequiresIdempotencyKey]
    [ProducesResponseType<CheckoutResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Checkout(CheckoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new CheckoutCart(request.CartId, request.PaymentMethod, request.Phone, request.Nuit), cancellationToken));
    }
}

[Route("api/orders")]
public sealed class OrdersController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetOrder(id), cancellationToken));

    [Authorize]
    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListMyOrders(), cancellationToken));

    /// <summary>Customers can cancel their own orders while the current state allows it.</summary>
    [Authorize]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid id, StatusChangeRequest request, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ChangeOrderStatus(id, "Cancel", request?.Note), cancellationToken));

    /// <summary>
    /// Fulfilment and payment transitions (back-office): RequestPayment, ConfirmPayment (note = gateway reference),
    /// FailPayment, RetryPayment, StartPreparing, Dispatch, Deliver, Cancel, Refund. Payment confirmations move to
    /// the Payments module's gateway callbacks in Phase 4.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.Backoffice)]
    [HttpPost("{id:guid}/transitions/{transition}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Transition(Guid id, string transition, StatusChangeRequest request, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ChangeOrderStatus(id, transition, request?.Note), cancellationToken));

    /// <summary>B2B: start next week's order as a copy of this one.</summary>
    [Authorize]
    [HttpPost("{id:guid}/reorder")]
    [ProducesResponseType<DraftOrderDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reorder(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ReorderAsDraft(id), cancellationToken));
}

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
        return OkOrProblem(await mediator.Send(new SubmitDraftOrder(id, request.PaymentMethod, request.Phone, request.Nuit), cancellationToken));
    }
}
