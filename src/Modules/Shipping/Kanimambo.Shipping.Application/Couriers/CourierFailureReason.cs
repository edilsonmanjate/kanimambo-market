namespace Kanimambo.Shipping.Application.Couriers;

public enum CourierFailureReason
{
    DestinationNotServed,
    Overweight,
    InvalidRecipient,
    Rejected,
    Unavailable,
}
