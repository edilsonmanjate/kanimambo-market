namespace Kanimambo.Payments.Domain;

public enum PaymentStatus
{
    Initiated,
    AwaitingCustomerPin,
    Confirmed,
    Failed,
    TimedOut,
}
