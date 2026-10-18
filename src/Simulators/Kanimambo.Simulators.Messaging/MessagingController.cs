using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.Messaging;

[ApiController]
public sealed class MessagingController(Inbox inbox) : ControllerBase
{
    /// <summary>Simulated SMS gateway: one part only — GSM-7 text, at most 160 characters.</summary>
    [HttpPost("sms/v1/messages")]
    public IActionResult Sms(SmsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Authorized())
        {
            return Unauthorized();
        }

        if (request.To.EndsWith("0201", StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "SMSC_UNAVAILABLE" });
        }

        var septets = Gsm7.Septets(request.Text);
        if (septets is null)
        {
            return BadRequest(new { error = "NOT_GSM7", message = "The text has characters outside the GSM-7 alphabet." });
        }

        if (septets > 160)
        {
            return BadRequest(new { error = "TOO_LONG", message = $"{septets} characters; one SMS carries 160." });
        }

        return Accepted(new { messageId = inbox.Add("sms", request.To, request.Text).Id, parts = 1 });
    }

    /// <summary>Simulated WhatsApp business API.</summary>
    [HttpPost("whatsapp/v1/messages")]
    public IActionResult WhatsApp(WhatsAppRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Authorized())
        {
            return Unauthorized();
        }

        if (request.To.EndsWith("0202", StringComparison.Ordinal))
        {
            return NotFound(new { error = "NOT_ON_WHATSAPP" });
        }

        return request.Body.Length > 4096
            ? BadRequest(new { error = "TOO_LONG" })
            : Accepted(new { id = inbox.Add("whatsapp", request.To, request.Body).Id });
    }

    /// <summary>Simulated transactional e-mail service.</summary>
    [HttpPost("email/v1/send")]
    public IActionResult Email(EmailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Authorized()
            ? Accepted(new { id = inbox.Add("email", request.To, request.Html, request.Subject).Id })
            : Unauthorized();
    }

    /// <summary>What a phone or mailbox received (for demos and tests).</summary>
    [HttpGet("inbox")]
    public IReadOnlyList<DeliveredMessage> Read([FromQuery] string? to) => inbox.For(to);

    private bool Authorized() => Request.Headers.ContainsKey(MessagingSimulator.ApiKeyHeader);
}
