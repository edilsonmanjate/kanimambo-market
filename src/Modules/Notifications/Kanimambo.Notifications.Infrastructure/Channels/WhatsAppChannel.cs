using Kanimambo.Notifications.Application.Messages;
using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Infrastructure.Channels;

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
