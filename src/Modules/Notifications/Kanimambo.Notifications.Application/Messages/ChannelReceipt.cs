namespace Kanimambo.Notifications.Application.Messages;

/// <summary>What a channel reports back: the provider's message id and the text exactly as it was sent.</summary>
public sealed record ChannelReceipt(string ProviderMessageId, string RenderedText);
