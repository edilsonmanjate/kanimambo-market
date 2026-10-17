using Kanimambo.Cart.Application;
using Kanimambo.Cart.Application.Pricing;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Cart.Api.Controllers;

public sealed record OpenCartRequest(string DeliveryCity);

public sealed record CartLineRequest(int Quantity);

public sealed record DeliveryCityRequest(string City);

/// <summary>
/// Carts for guests (the cart id is the key — keep it on the device) and signed-in customers (owner only).
/// Every change is snapshotted; <c>undo</c> and <c>snapshots/{version}/restore</c> go back in that history.
/// </summary>
[Route("api/carts")]
public sealed class CartsController(IMediator mediator) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<CartDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Open(OpenCartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CreatedOrProblem(await mediator.Send(new OpenCart(request.DeliveryCity), cancellationToken), nameof(Get), cart => new { id = cart.Id });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetCart(id), cancellationToken));

    /// <summary>Sets the quantity of a product (0 removes it).</summary>
    [HttpPut("{id:guid}/lines/{productId:guid}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetLine(Guid id, Guid productId, CartLineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new SetCartLine(id, productId, request.Quantity), cancellationToken));
    }

    [HttpDelete("{id:guid}/lines/{productId:guid}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveLine(Guid id, Guid productId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new RemoveCartLine(id, productId), cancellationToken));

    [HttpPut("{id:guid}/delivery-city")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeDeliveryCity(Guid id, DeliveryCityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new ChangeDeliveryCity(id, request.City), cancellationToken));
    }

    /// <summary>Prices the cart in MZN with the best promotion; pass the intended payment method to preview its promotions.</summary>
    [HttpGet("{id:guid}/pricing")]
    [ProducesResponseType<CartPricingDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Price(Guid id, [FromQuery] PaymentMethod paymentMethod, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new PriceCart(id, paymentMethod), cancellationToken));

    [HttpGet("{id:guid}/snapshots")]
    [ProducesResponseType<IReadOnlyList<CartSnapshotDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Snapshots(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListCartSnapshots(id), cancellationToken));

    [HttpPost("{id:guid}/snapshots/{version:int}/restore")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Restore(Guid id, int version, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new RestoreCartSnapshot(id, version), cancellationToken));

    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Undo(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new UndoCartChange(id), cancellationToken));
}
