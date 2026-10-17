using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

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

public enum ConsignmentStatus
{
    Requested,
    Booked,
    InTransit,
    ReadyForCollection,
    Delivered,
    Failed,
}

public sealed record ParcelStatusChanged(
    Guid ConsignmentId, Guid OrderId, long OrderNumber, Guid ParcelId, ParcelStatus Status, string Courier, string? TrackingCode,
    string RecipientPhone, string? Detail, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

public sealed record ConsignmentDelivered(Guid ConsignmentId, Guid OrderId, long OrderNumber, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

public sealed record ConsignmentFailed(Guid ConsignmentId, Guid OrderId, long OrderNumber, Guid ParcelId, string Reason, DateTimeOffset OccurredAt)
    : DomainEvent(OccurredAt);

public sealed record ParcelTrackingEntry(ParcelStatus Status, DateTimeOffset At, string? Detail);

/// <summary>What one parcel needs to be shipped, decided when the order is dispatched.</summary>
public sealed record ParcelPlan(Guid SellerId, string OriginCity, decimal WeightKg, string Courier, string? Vehicle, Money CashToCollect);

/// <summary>One seller's goods for an order, carried by one courier under one tracking code.</summary>
public sealed class Parcel : Entity<Guid>
{
    private readonly List<ParcelTrackingEntry> _tracking = [];
    private decimal _cashToCollectMzn;

    private Parcel()
    {
    }

    internal Parcel(Guid id, string reference, ParcelPlan plan, DateTimeOffset now)
        : base(id)
    {
        Reference = reference;
        SellerId = plan.SellerId;
        OriginCity = plan.OriginCity;
        WeightKg = plan.WeightKg;
        Courier = plan.Courier;
        Vehicle = plan.Vehicle;
        _cashToCollectMzn = plan.CashToCollect.Amount;
        Status = ParcelStatus.Requested;
        _tracking.Add(new ParcelTrackingEntry(ParcelStatus.Requested, now, null));
    }

    /// <summary>Our reference at the courier (<c>KM-1002-1</c>): deterministic, so a retried booking is recognised as the same one.</summary>
    public string Reference { get; private set; } = string.Empty;

    public Guid SellerId { get; private set; }

    public string OriginCity { get; private set; } = string.Empty;

    public decimal WeightKg { get; private set; }

    public string Courier { get; private set; } = string.Empty;

    /// <summary>Same-day only: motorbike or txopela.</summary>
    public string? Vehicle { get; private set; }

    /// <summary>Cash on delivery: what the courier collects with this parcel (zero for prepaid orders).</summary>
    public Money CashToCollect => Money.Mzn(_cashToCollectMzn);

    public ParcelStatus Status { get; private set; }

    public string? TrackingCode { get; private set; }

    public string? FailureReason { get; private set; }

    public IReadOnlyList<ParcelTrackingEntry> Tracking => _tracking.AsReadOnly();

    internal bool CanMoveTo(ParcelStatus next) =>
        Status is not (ParcelStatus.Delivered or ParcelStatus.Failed)
        && (next == ParcelStatus.Failed || next > Status);

    internal void MoveTo(ParcelStatus next, string? detail, DateTimeOffset now)
    {
        Status = next;
        _tracking.Add(new ParcelTrackingEntry(next, now, detail));
    }

    internal void RecordBooking(string trackingCode, DateTimeOffset now)
    {
        TrackingCode = trackingCode;
        MoveTo(ParcelStatus.Booked, trackingCode, now);
    }

    internal void RecordFailure(string reason, DateTimeOffset now)
    {
        FailureReason = reason;
        MoveTo(ParcelStatus.Failed, reason, now);
    }
}

/// <summary>
/// The delivery of one order: one parcel per seller, each with its own courier. The order counts as delivered
/// only when every parcel is. Being one aggregate, concurrent courier webhooks for sibling parcels cannot both
/// miss the "last parcel delivered" moment (optimistic concurrency makes one of them retry).
/// </summary>
public sealed class Consignment : AggregateRoot<Guid>
{
    private readonly List<Parcel> _parcels = [];

    private Consignment()
    {
    }

    private Consignment(Guid id, Guid orderId, long orderNumber, string method, string destinationCity, string? district,
        string? pickupPointId, string recipientPhone, DateTimeOffset now)
        : base(id)
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        Method = method;
        DestinationCity = destinationCity;
        District = district;
        PickupPointId = pickupPointId;
        RecipientPhone = recipientPhone;
        Status = ConsignmentStatus.Requested;
        CreatedAt = UpdatedAt = now;
    }

    public Guid OrderId { get; private set; }

    public long OrderNumber { get; private set; }

    /// <summary>Shipping method name chosen at checkout (SameDay, Courier, Pickup…).</summary>
    public string Method { get; private set; } = string.Empty;

    public string DestinationCity { get; private set; } = string.Empty;

    public string? District { get; private set; }

    public string? PickupPointId { get; private set; }

    public string RecipientPhone { get; private set; } = string.Empty;

    public ConsignmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<Parcel> Parcels => _parcels.AsReadOnly();

    public bool IsPickup => PickupPointId is not null;

    public static Result<Consignment> Create(
        Guid orderId,
        long orderNumber,
        string method,
        string destinationCity,
        string? district,
        string? pickupPointId,
        string recipientPhone,
        IReadOnlyList<ParcelPlan> parcels,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        if (parcels.Count == 0)
        {
            return Error.BusinessRule("shipping.no_parcels", "A consignment needs at least one parcel.");
        }

        var consignment = new Consignment(Guid.NewGuid(), orderId, orderNumber, method, destinationCity, district, pickupPointId, recipientPhone, now);
        for (var i = 0; i < parcels.Count; i++)
        {
            consignment._parcels.Add(new Parcel(Guid.NewGuid(), $"KM-{orderNumber}-{i + 1}", parcels[i], now));
        }

        return consignment;
    }

    public Parcel? FindParcel(string reference) => _parcels.Find(p => p.Reference == reference);

    public Result RecordBooking(Guid parcelId, string trackingCode, DateTimeOffset now) =>
        Change(parcelId, ParcelStatus.Booked, trackingCode, now, p => p.RecordBooking(trackingCode, now));

    public Result RecordBookingRefused(Guid parcelId, string reason, DateTimeOffset now) =>
        Change(parcelId, ParcelStatus.Failed, reason, now, p => p.RecordFailure(reason, now));

    /// <summary>
    /// A courier update. A parcel delivered to a pickup point is only ready for collection; out-of-order or repeated
    /// updates (couriers retry webhooks) are refused and leave everything unchanged.
    /// </summary>
    public Result Track(Guid parcelId, ParcelStatus status, string? detail, DateTimeOffset now)
    {
        var next = status == ParcelStatus.Delivered && IsPickup ? ParcelStatus.ReadyForCollection : status;
        return next == ParcelStatus.Failed
            ? Change(parcelId, next, detail, now, p => p.RecordFailure(detail ?? "Delivery failed.", now))
            : Change(parcelId, next, detail, now, p => p.MoveTo(next, detail, now));
    }

    /// <summary>The customer collected everything at the pickup point (confirmed by staff there).</summary>
    public Result Collect(DateTimeOffset now)
    {
        if (!IsPickup || Status != ConsignmentStatus.ReadyForCollection)
        {
            return Error.Conflict("shipping.not_ready_for_collection", $"Consignment for KM-{OrderNumber} is {Status}, not ready for collection.");
        }

        foreach (var parcel in _parcels)
        {
            Change(parcel.Id, ParcelStatus.Delivered, "Collected at the pickup point.", now, p => p.MoveTo(ParcelStatus.Delivered, "Collected at the pickup point.", now));
        }

        return Result.Success();
    }

    private Result Change(Guid parcelId, ParcelStatus next, string? detail, DateTimeOffset now, Action<Parcel> apply)
    {
        var parcel = _parcels.Find(p => p.Id == parcelId);
        if (parcel is null)
        {
            return Error.NotFound("shipping.parcel_not_found", $"No parcel {parcelId} in consignment {Id}.");
        }

        if (!parcel.CanMoveTo(next))
        {
            return Error.Conflict("shipping.stale_update", $"Parcel {parcel.Reference} is {parcel.Status}; ignoring {next}.");
        }

        apply(parcel);
        UpdatedAt = now;
        Raise(new ParcelStatusChanged(Id, OrderId, OrderNumber, parcel.Id, parcel.Status, parcel.Courier, parcel.TrackingCode, RecipientPhone, detail, now));

        var previous = Status;
        Status = Derive();
        if (Status != previous)
        {
            if (Status == ConsignmentStatus.Delivered)
            {
                Raise(new ConsignmentDelivered(Id, OrderId, OrderNumber, now));
            }
            else if (Status == ConsignmentStatus.Failed)
            {
                Raise(new ConsignmentFailed(Id, OrderId, OrderNumber, parcel.Id, parcel.FailureReason ?? "Delivery failed.", now));
            }
        }

        return Result.Success();
    }

    private ConsignmentStatus Derive()
    {
        if (_parcels.Exists(p => p.Status == ParcelStatus.Failed))
        {
            return ConsignmentStatus.Failed;
        }

        var slowest = _parcels.Min(p => p.Status);
        return slowest switch
        {
            ParcelStatus.Delivered => ConsignmentStatus.Delivered,
            ParcelStatus.ReadyForCollection => ConsignmentStatus.ReadyForCollection,
            ParcelStatus.Requested => ConsignmentStatus.Requested,
            ParcelStatus.Booked when _parcels.TrueForAll(p => p.Status == ParcelStatus.Booked) => ConsignmentStatus.Booked,
            _ => ConsignmentStatus.InTransit,
        };
    }
}
