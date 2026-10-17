using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Security;
using Kanimambo.Shipping.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Shipping.Api.Controllers;

[Route("api/shipping")]
public sealed class ShippingController(IMediator mediator) : ApiControllerBase
{
    public const string SignatureHeader = "X-Signature";

    /// <summary>Delivery options for a cart (same-day, courier, pickup), cheapest first. Pass the district for same-day zones.</summary>
    [HttpGet("options")]
    [ProducesResponseType<IReadOnlyList<ShippingOptionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Options([FromQuery] Guid cartId, [FromQuery] string? district, [FromQuery] string? pickupPointId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetShippingOptions(cartId, district, pickupPointId), cancellationToken));

    [HttpGet("pickup-points")]
    [ProducesResponseType<IReadOnlyList<PickupPointDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PickupPoints([FromQuery] string? city, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListPickupPoints(city), cancellationToken));

    /// <summary>The delivery of an order: one parcel per seller, with each courier's tracking history.</summary>
    [Authorize(Policy = AuthorizationPolicies.Backoffice)]
    [HttpGet("consignments")]
    [ProducesResponseType<ConsignmentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForOrder([FromQuery] Guid orderId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetConsignmentForOrder(orderId), cancellationToken));

    /// <summary>Pickup-point staff confirm the customer collected the order; the order then becomes delivered.</summary>
    [Authorize(Policy = AuthorizationPolicies.Backoffice)]
    [HttpPost("consignments/{id:guid}/collected")]
    [ProducesResponseType<ConsignmentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Collected(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new CollectConsignment(id), cancellationToken));

    /// <summary>
    /// Tracking webhooks from couriers. Anonymous, but every body must carry the courier's HMAC signature; the
    /// courier's adapter verifies and translates it.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("webhooks/{courierName}")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Webhook(string courierName, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        return NoContentOrProblem(await mediator.Send(
            new HandleCourierWebhook(courierName, body, Request.Headers[SignatureHeader].FirstOrDefault()), cancellationToken));
    }
}
