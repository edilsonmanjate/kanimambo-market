namespace Kanimambo.Shipping.Domain;

public enum ConsignmentStatus
{
    Requested,
    Booked,
    InTransit,
    ReadyForCollection,
    Delivered,
    Failed,
}
