using Kanimambo.SharedKernel.Domain;
using Kanimambo.Shipping.Domain;
using static Kanimambo.Shipping.UnitTests.TestShipping;

namespace Kanimambo.Shipping.UnitTests.Domain;

public sealed class ConsignmentTests
{
    private static Consignment Create(string? pickupPoint = null, int parcels = 1)
    {
        var plans = Enumerable.Range(0, parcels)
            .Select(_ => new ParcelPlan(Guid.NewGuid(), "Maputo", 3m, "Txova", "Motorbike", Money.Zero(Currency.Mzn)))
            .ToList();
        var consignment = Consignment.Create(Guid.NewGuid(), 1002, pickupPoint is null ? "SameDay" : "Pickup", "Maputo", "KaMpfumo",
            pickupPoint, "258841234567", plans, Morning).Value;
        foreach (var parcel in consignment.Parcels)
        {
            consignment.RecordBooking(parcel.Id, $"TX-{parcel.Reference}", Morning);
        }

        consignment.ClearDomainEvents();
        return consignment;
    }

    [Fact]
    public void Parcels_get_deterministic_references_per_order()
    {
        var consignment = Create(parcels: 2);

        consignment.Parcels.Select(p => p.Reference).ShouldBe(["KM-1002-1", "KM-1002-2"]);
        consignment.FindParcel("KM-1002-2").ShouldBe(consignment.Parcels[1]);
    }

    [Fact]
    public void A_parcel_moves_forward_and_the_consignment_follows()
    {
        var consignment = Create();
        var parcel = consignment.Parcels[0];

        consignment.Track(parcel.Id, ParcelStatus.PickedUp, null, Morning).IsSuccess.ShouldBeTrue();
        consignment.Status.ShouldBe(ConsignmentStatus.InTransit);
        consignment.Track(parcel.Id, ParcelStatus.Delivered, null, Morning).IsSuccess.ShouldBeTrue();

        consignment.Status.ShouldBe(ConsignmentStatus.Delivered);
        parcel.Tracking.Select(t => t.Status).ShouldBe([ParcelStatus.Requested, ParcelStatus.Booked, ParcelStatus.PickedUp, ParcelStatus.Delivered]);
        consignment.DomainEvents.OfType<ConsignmentDelivered>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(ParcelStatus.PickedUp)]   // a repeated webhook
    [InlineData(ParcelStatus.Booked)]     // an older event arriving late
    public void Repeated_or_out_of_order_updates_are_refused_and_change_nothing(ParcelStatus late)
    {
        var consignment = Create();
        var parcel = consignment.Parcels[0];
        consignment.Track(parcel.Id, ParcelStatus.PickedUp, null, Morning);
        consignment.ClearDomainEvents();

        var result = consignment.Track(parcel.Id, late, null, Morning);

        result.Error.Code.ShouldBe("shipping.stale_update");
        parcel.Status.ShouldBe(ParcelStatus.PickedUp);
        consignment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_moves_after_delivery()
    {
        var consignment = Create();
        var parcel = consignment.Parcels[0];
        consignment.Track(parcel.Id, ParcelStatus.Delivered, null, Morning);

        consignment.Track(parcel.Id, ParcelStatus.Failed, "late failure", Morning).IsFailure.ShouldBeTrue();
        consignment.Status.ShouldBe(ConsignmentStatus.Delivered);
    }

    [Fact]
    public void The_order_is_delivered_only_when_every_parcel_is()
    {
        var consignment = Create(parcels: 2);

        consignment.Track(consignment.Parcels[0].Id, ParcelStatus.Delivered, null, Morning);
        consignment.Status.ShouldBe(ConsignmentStatus.InTransit);
        consignment.DomainEvents.OfType<ConsignmentDelivered>().ShouldBeEmpty();

        consignment.Track(consignment.Parcels[1].Id, ParcelStatus.Delivered, null, Morning);
        consignment.Status.ShouldBe(ConsignmentStatus.Delivered);
        consignment.DomainEvents.OfType<ConsignmentDelivered>().ShouldHaveSingleItem();
    }

    [Fact]
    public void A_failed_parcel_fails_the_consignment_with_its_reason()
    {
        var consignment = Create(parcels: 2);

        consignment.Track(consignment.Parcels[1].Id, ParcelStatus.Failed, "Destinatário ausente", Morning);

        consignment.Status.ShouldBe(ConsignmentStatus.Failed);
        consignment.Parcels[1].FailureReason.ShouldBe("Destinatário ausente");
        consignment.DomainEvents.OfType<ConsignmentFailed>().ShouldHaveSingleItem().Reason.ShouldBe("Destinatário ausente");
    }

    [Fact]
    public void A_parcel_delivered_to_a_pickup_point_waits_for_collection()
    {
        var consignment = Create(pickupPoint: "maputo-baixa");

        consignment.Track(consignment.Parcels[0].Id, ParcelStatus.Delivered, null, Morning);

        consignment.Status.ShouldBe(ConsignmentStatus.ReadyForCollection);
        consignment.DomainEvents.OfType<ConsignmentDelivered>().ShouldBeEmpty();

        consignment.Collect(Morning).IsSuccess.ShouldBeTrue();
        consignment.Status.ShouldBe(ConsignmentStatus.Delivered);
        consignment.DomainEvents.OfType<ConsignmentDelivered>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Collection_needs_the_parcels_at_the_point()
    {
        Create(pickupPoint: "maputo-baixa").Collect(Morning).Error.Code.ShouldBe("shipping.not_ready_for_collection");
        Create().Collect(Morning).Error.Code.ShouldBe("shipping.not_ready_for_collection");
    }

    [Fact]
    public void A_refused_booking_fails_the_parcel()
    {
        var consignment = Consignment.Create(Guid.NewGuid(), 1003, "Courier", "Lichinga", null, null, "258841234567",
            [new ParcelPlan(Guid.NewGuid(), "Maputo", 3m, "Mbila", null, Money.Zero(Currency.Mzn))], Morning).Value;

        consignment.RecordBookingRefused(consignment.Parcels[0].Id, "Mbila: Destino não servido", Morning);

        consignment.Status.ShouldBe(ConsignmentStatus.Failed);
        consignment.DomainEvents.OfType<ConsignmentFailed>().ShouldHaveSingleItem();
    }
}
