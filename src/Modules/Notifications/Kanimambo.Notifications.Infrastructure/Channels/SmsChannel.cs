using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Infrastructure.Channels;

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
