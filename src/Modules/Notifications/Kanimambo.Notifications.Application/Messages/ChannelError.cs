using Kanimambo.Notifications.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>A delivery failure; transient ones (gateway down) are worth retrying later, the others are not.</summary>
public sealed record ChannelError : Error
{
    public ChannelError(ChannelKind channel, string code, string message, bool transient)
        : base($"notifications.{channel.ToString().ToLowerInvariant()}_{code}", message, transient ? ErrorType.Unavailable : ErrorType.BusinessRule)
    {
        Channel = channel;
        IsTransient = transient;
    }

    public ChannelKind Channel { get; }

    public bool IsTransient { get; }
}
