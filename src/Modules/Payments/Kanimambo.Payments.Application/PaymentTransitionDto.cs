namespace Kanimambo.Payments.Application;

public sealed record PaymentTransitionDto(string From, string To, DateTimeOffset At, string? Detail);
