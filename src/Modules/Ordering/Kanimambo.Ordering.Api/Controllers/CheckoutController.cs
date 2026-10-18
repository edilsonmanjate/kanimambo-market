using Kanimambo.Ordering.Application;
using Kanimambo.Ordering.Application.Checkout;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Idempotency;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Ordering.Api.Controllers;

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
        return OkOrProblem(await mediator.Send(new CheckoutCart(request.CartId, request.PaymentMethod, request.Phone, request.Nuit,
            new DeliveryChoice(request.DeliveryMethod, request.DeliveryDistrict, request.PickupPointId)), cancellationToken));
    }
}
