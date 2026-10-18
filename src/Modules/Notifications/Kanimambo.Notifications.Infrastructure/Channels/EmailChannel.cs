using System.Text.Encodings.Web;
using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Infrastructure.Channels;

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
