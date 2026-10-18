namespace Kanimambo.Shipping.Domain;

/// <summary>
/// Where a parcel is. The lifecycle is a straight line (each status only ever moves forward), so a rank is
/// enough here — unlike orders and payments, there is no status-dependent behaviour worth a State pattern.
/// </summary>
public enum ParcelStatus
{
    Requested = 0,
    Booked = 1,
    PickedUp = 2,
    InTransit = 3,
    ReadyForCollection = 4,
    Delivered = 5,
    Failed = 99,
}
