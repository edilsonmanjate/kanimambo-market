using System.Collections.Concurrent;

namespace Kanimambo.Simulators.Messaging;

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
