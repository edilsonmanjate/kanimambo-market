namespace Kanimambo.Ordering.Domain;

public enum OrderStatus
{
    Pending,
    AwaitingPayment,
    Paid,
    Preparing,
    OutForDelivery,
    Delivered,
    Cancelled,
    PaymentFailed,
    Refunded,
}
