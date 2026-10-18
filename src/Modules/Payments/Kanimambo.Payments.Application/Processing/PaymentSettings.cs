namespace Kanimambo.Payments.Application.Processing;

/// <summary>Timing and addressing for payments, from configuration.</summary>
public sealed record PaymentSettings(TimeSpan PinTimeout, Uri CallbackBaseAddress);
