namespace Kanimambo.Payments.Domain;

public sealed record PaymentTransition(PaymentStatus From, PaymentStatus To, DateTimeOffset At, string? Detail);
