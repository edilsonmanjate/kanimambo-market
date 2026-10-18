using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>
/// Bridge — the <em>implementor</em> side: a way to deliver any message. Each channel decides how content is rendered
/// for it (an SMS keeps the headline, in GSM-7, within 160 characters; WhatsApp adds the details; e-mail builds a
/// page) and talks to its own gateway. Channels know nothing about orders, payments or invoices.
/// </summary>
public interface INotificationChannel
{
    ChannelKind Kind { get; }

    Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, MessageContent content, CancellationToken cancellationToken);
}
