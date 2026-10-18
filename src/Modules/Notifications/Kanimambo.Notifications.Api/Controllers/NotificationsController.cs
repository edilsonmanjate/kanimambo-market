using Kanimambo.Notifications.Application;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Notifications.Api.Controllers;

/// <summary>Channels in order of preference, e.g. <c>["WhatsApp", "Sms"]</c>; e-mail needs an address.</summary>
public sealed record ContactPreferencesRequest(string? Email, IReadOnlyList<string> Channels);

[Route("api/notifications")]
public sealed class NotificationsController(IMediator mediator) : ApiControllerBase
{
    /// <summary>How the customer behind this phone number wants to be contacted (default: SMS).</summary>
    [Authorize]
    [HttpGet("preferences/{phone}")]
    [ProducesResponseType<ContactPreferencesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Preferences(string phone, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetContactPreferences(phone), cancellationToken));

    /// <summary>Choose channels for a phone number; the first account to register a number owns its preferences.</summary>
    [Authorize]
    [HttpPut("preferences/{phone}")]
    [ProducesResponseType<ContactPreferencesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPreferences(string phone, ContactPreferencesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OkOrProblem(await mediator.Send(new SetContactPreferences(phone, request.Email, request.Channels), cancellationToken));
    }

    /// <summary>What was sent (or failed) to a phone number / e-mail address or for an order — for customer support.</summary>
    [Authorize(Policy = AuthorizationPolicies.Backoffice)]
    [HttpGet("sent")]
    [ProducesResponseType<IReadOnlyList<SentNotificationDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Sent([FromQuery] string? recipient, [FromQuery] long? orderNumber, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ListSentNotifications(recipient, orderNumber), cancellationToken));
}
