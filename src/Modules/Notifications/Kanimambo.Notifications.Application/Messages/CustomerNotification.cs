using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>
/// Bridge — the <em>abstraction</em> side: a kind of message the business sends. It decides what to say and holds a
/// reference to the channel that says it. Message types and channels vary independently: 4 messages × 3 channels
/// are 7 classes, not 12, and a new channel (USSD, push) or a new message is one more class.
/// </summary>
public abstract class CustomerNotification(INotificationChannel channel)
{
    protected INotificationChannel Channel { get; } = channel;

    public abstract string Type { get; }

    public abstract MessageContent Compose();

    public Task<Result<ChannelReceipt>> SendAsync(Recipient recipient, CancellationToken cancellationToken) =>
        Channel.SendAsync(recipient, Compose(), cancellationToken);
}
