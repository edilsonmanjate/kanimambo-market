namespace Kanimambo.Shipping.Application;

/// <summary>Where couriers send their tracking webhooks (this API's public address).</summary>
public sealed record ShippingSettings(Uri WebhookBaseAddress);
