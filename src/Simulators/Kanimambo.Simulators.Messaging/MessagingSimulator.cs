using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.Messaging;

/// <summary>
/// Local stand-in for an SMS gateway, a WhatsApp business API and an e-mail service (simulated formats, not any
/// provider's real API). It enforces what real gateways enforce — an SMS is GSM-7 text of at most 160 characters —
/// and keeps every accepted message in an inbox that can be read back (<c>GET /inbox?to=</c>).
/// Recipient phones ending in 0201 hit an SMS gateway outage (503); ending in 0202 are not on WhatsApp.
/// </summary>
public static class MessagingSimulator
{
    public const string ApiKeyHeader = "X-Api-Key";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();
        builder.Services.AddSingleton<Inbox>();
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.MapControllers();
        app.MapDefaultEndpoints();
        await app.RunAsync();
    }
}

public sealed record DeliveredMessage(string Id, string Channel, string To, string Text, string? Subject, DateTimeOffset At);

/// <summary>Every message the "providers" accepted, newest last.</summary>
public sealed class Inbox
{
    private readonly ConcurrentQueue<DeliveredMessage> _messages = new();

    public DeliveredMessage Add(string channel, string to, string text, string? subject = null)
    {
        var message = new DeliveredMessage($"{channel[..2].ToUpperInvariant()}-{Guid.NewGuid():N}"[..14], channel, to, text, subject, DateTimeOffset.UtcNow);
        _messages.Enqueue(message);
        return message;
    }

    public IReadOnlyList<DeliveredMessage> For(string? to) => [.. _messages.Where(m => to is null || m.To == to)];
}

/// <summary>The GSM 03.38 default alphabet (and its extension table, two septets each): what an SMS can carry as 160 characters.</summary>
public static class Gsm7
{
    public const string Basic = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    public const string Extension = "^{}\\[~]|€\f";

    /// <summary>Length in septets, or null when the text has characters outside GSM-7.</summary>
    public static int? Septets(string text)
    {
        var septets = 0;
        foreach (var c in text)
        {
            if (Basic.Contains(c, StringComparison.Ordinal))
            {
                septets++;
            }
            else if (Extension.Contains(c, StringComparison.Ordinal))
            {
                septets += 2;
            }
            else
            {
                return null;
            }
        }

        return septets;
    }
}

public sealed record SmsRequest(string To, string Sender, string Text);

public sealed record WhatsAppRequest(string To, string Body);

public sealed record EmailRequest(string To, string Subject, string Html);

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
