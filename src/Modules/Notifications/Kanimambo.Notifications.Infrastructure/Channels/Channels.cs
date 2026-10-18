using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Infrastructure.Channels;

// The gateway endpoints below are the local simulator's (Kanimambo.Simulators.Messaging), not any provider's real API.
// A real SMS aggregator, the WhatsApp Business API or an e-mail service needs its own adapter and credentials.

public sealed class GatewayOptions
{
    public Uri BaseAddress { get; set; } = new("https+http://messaging");

    /// <summary>Local development value only; real keys come from a vault.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The alphanumeric sender id shown on the customer's phone (max 11 characters).</summary>
    public string SmsSender { get; set; } = "KANIMAMBO";

    /// <summary>Per message; a gateway slower than this counts as down and the next channel is tried.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// SMS text rules: one SMS carries 160 characters of the GSM-7 alphabet. Portuguese letters outside it (ã, õ, ç, á, ê…)
/// would switch the whole message to UCS-2 and 70 characters, so they are written without the accent instead.
/// </summary>
public static class Gsm7Text
{
    public const int MaxLength = 160;
    private const string Basic = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string Extension = "^{}\\[~]|€";

    private static readonly Dictionary<char, string> Replacements = new()
    {
        ['á'] = "a", ['â'] = "a", ['ã'] = "a", ['ç'] = "c", ['ê'] = "e", ['í'] = "i", ['ó'] = "o", ['ô'] = "o", ['õ'] = "o", ['ú'] = "u",
        ['Á'] = "A", ['À'] = "A", ['Â'] = "A", ['Ã'] = "A", ['Ê'] = "E", ['Í'] = "I", ['Ó'] = "O", ['Ô'] = "O", ['Õ'] = "O", ['Ú'] = "U",
        ['—'] = "-", ['–'] = "-", ['“'] = "\"", ['”'] = "\"", ['‘'] = "'", ['’'] = "'", ['…'] = "...", [' '] = " ", ['º'] = "o", ['ª'] = "a",
    };

    /// <summary>Converts to GSM-7; anything it cannot represent becomes '?'.</summary>
    public static string Convert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length);
        // By code point, not UTF-16 unit: an emoji is one character to the customer, so one '?'.
        foreach (var rune in text.EnumerateRunes())
        {
            if (!rune.IsBmp)
            {
                builder.Append('?');
                continue;
            }

            var c = (char)rune.Value;
            if (Basic.Contains(c, StringComparison.Ordinal) || Extension.Contains(c, StringComparison.Ordinal))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(Replacements.GetValueOrDefault(c, "?"));
            }
        }

        return builder.ToString();
    }

    /// <summary>Characters as the network counts them: extension characters (€, [, ]…) take two.</summary>
    public static int Length(string gsmText)
    {
        ArgumentNullException.ThrowIfNull(gsmText);
        return gsmText.Sum(c => Extension.Contains(c, StringComparison.Ordinal) ? 2 : 1);
    }

    /// <summary>Shortens at a word boundary, ending with "...", so the text fits one SMS.</summary>
    public static string Fit(string gsmText)
    {
        if (Length(gsmText) <= MaxLength)
        {
            return gsmText;
        }

        var cut = gsmText;
        while (Length(cut) > MaxLength - 3)
        {
            var space = cut.LastIndexOf(' ');
            cut = space > 0 ? cut[..space] : cut[..^1];
        }

        return cut.TrimEnd(' ', ',', '.', ':', ';') + "...";
    }
}

internal static class GatewayCall
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<Result<string>> PostAsync(HttpClient http, GatewayOptions options, ChannelKind channel, string path, object body,
        Func<JsonElement, string> readId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(body, options: Json) };
            request.Headers.Add("X-Api-Key", options.ApiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            var payload = response.Content.Headers.ContentLength is 0 ? default : await response.Content.ReadFromJsonAsync<JsonElement>(Json, cancellationToken);

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new ChannelError(channel, "unavailable", $"{channel} gateway answered {(int)response.StatusCode}.", transient: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                var code = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("error", out var e) ? e.GetString() ?? "rejected" : "rejected";
                return new ChannelError(channel, code.ToLowerInvariant(), $"{channel} refused the message: {code}.", transient: false);
            }

            return readId(payload);
        }
        catch (HttpRequestException exception)
        {
            return new ChannelError(channel, "unavailable", exception.Message, transient: true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ChannelError(channel, "unavailable", $"{channel} gateway did not answer in time.", transient: true);
        }
    }
}

/// <summary>Bridge implementor: SMS. Only the headline (and the link if it still fits), in GSM-7, within 160 characters.</summary>
public sealed class SmsChannel(HttpClient http, GatewayOptions options) : INotificationChannel
{
    public ChannelKind Kind => ChannelKind.Sms;

    public static string Render(MessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var headline = Gsm7Text.Convert(content.Headline);
        if (content.Link is { } link)
        {
            var withLink = $"{headline} {link.Host}{link.AbsolutePath}";
            if (Gsm7Text.Length(withLink) <= Gsm7Text.MaxLength)
            {
                return withLink;
            }
        }

        return Gsm7Text.Fit(headline);
    }

    public async Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, MessageContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        var text = Render(content);
        var sent = await GatewayCall.PostAsync(http, options, Kind, "sms/v1/messages", new { to = recipient.Phone, sender = options.SmsSender, text },
            p => p.GetProperty("messageId").GetString()!, cancellationToken);
        return sent.IsSuccess ? new ChannelReceipt(sent.Value, text) : sent.Error;
    }
}

/// <summary>Bridge implementor: WhatsApp. Bold subject, the essentials, the details and the link.</summary>
public sealed class WhatsAppChannel(HttpClient http, GatewayOptions options) : INotificationChannel
{
    public ChannelKind Kind => ChannelKind.WhatsApp;

    public static string Render(MessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return $"*{content.Subject}*\n{content.Headline}\n{content.Details}{(content.Link is null ? string.Empty : $"\n{content.Link}")}";
    }

    public async Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, MessageContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        var body = Render(content);
        var sent = await GatewayCall.PostAsync(http, options, Kind, "whatsapp/v1/messages", new { to = recipient.Phone, body },
            p => p.GetProperty("id").GetString()!, cancellationToken);
        return sent.IsSuccess ? new ChannelReceipt(sent.Value, body) : sent.Error;
    }
}

/// <summary>Bridge implementor: e-mail. Subject line plus a small HTML page.</summary>
public sealed class EmailChannel(HttpClient http, GatewayOptions options) : INotificationChannel
{
    public ChannelKind Kind => ChannelKind.Email;

    public static string Render(MessageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var html = HtmlEncoder.Default;
        var link = content.Link is null ? string.Empty : $"<p><a href=\"{html.Encode(content.Link.ToString())}\">Ver detalhes</a></p>";
        return $"<h2>{html.Encode(content.Subject)}</h2><p>{html.Encode(content.Headline)}</p><p>{html.Encode(content.Details)}</p>{link}<p>Kanimambo Market</p>";
    }

    public async Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, MessageContent content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(content);
        if (recipient.Email is null)
        {
            return new ChannelError(Kind, "no_address", "The customer has no e-mail address.", transient: false);
        }

        var page = Render(content);
        var sent = await GatewayCall.PostAsync(http, options, Kind, "email/v1/send", new { to = recipient.Email, subject = content.Subject, html = page },
            p => p.GetProperty("id").GetString()!, cancellationToken);
        return sent.IsSuccess ? new ChannelReceipt(sent.Value, $"{content.Subject}\n{page}") : sent.Error;
    }
}
