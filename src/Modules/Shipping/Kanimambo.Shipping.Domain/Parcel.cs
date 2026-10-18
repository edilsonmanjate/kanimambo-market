using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Shipping.Domain;

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
