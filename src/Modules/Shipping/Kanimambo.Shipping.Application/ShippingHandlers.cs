using Kanimambo.Cart.Contracts;
using Kanimambo.Catalog.Contracts;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Application.Couriers;
using Kanimambo.Shipping.Application.Pricing;
using Kanimambo.Shipping.Contracts;
using Kanimambo.Shipping.Domain;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Shipping.Application;

public sealed partial class ShippingHandlers(
    IConsignmentRepository consignments,
    ShippingStrategies strategies,
    ShippingOptions options,
    IShippingQuoter quoter,
    ICourierGatewayProvider couriers,
    ShippingSettings settings,
    ICartReader carts,
    ICatalogReader catalog,
    TimeProvider timeProvider,
    ILogger<ShippingHandlers> logger) :
    IRequestHandler<ShipOrder, Result>,
    IRequestHandler<HandleCourierWebhook, Result>,
    IRequestHandler<CollectConsignment, Result<ConsignmentDto>>,
    IRequestHandler<GetConsignmentForOrder, Result<ConsignmentDto>>,
    IRequestHandler<GetShippingOptions, Result<IReadOnlyList<ShippingOptionDto>>>,
    IRequestHandler<ListPickupPoints, Result<IReadOnlyList<PickupPointDto>>>
{
    public async ValueTask<Result> Handle(ShipOrder request, CancellationToken cancellationToken)
    {
        if (await consignments.FindByOrderAsync(request.OrderId, cancellationToken) is not null)
        {
            return Result.Success();
        }

        // Re-plan with the same strategy the customer chose at checkout, to know the courier and vehicle per parcel.
        var strategy = strategies.Find(request.Method);
        var legs = request.Parcels
            .Select(p => strategy.IsFailure
                ? Result<ShippingLeg>.Failure(strategy.Error)
                : strategy.Value.Quote(new ShipmentRoute(p.OriginCity, request.DestinationCity, request.District, request.PickupPointId, p.WeightKg)))
            .ToList();

        // Cash on delivery: the whole amount is collected with the first parcel.
        var plans = request.Parcels.Select((p, i) => new ParcelPlan(p.SellerId, p.OriginCity, p.WeightKg,
            legs[i].IsSuccess ? legs[i].Value.Courier : "none", legs[i].IsSuccess ? legs[i].Value.Vehicle : null,
            Money.Mzn(i == 0 ? request.CashToCollectMzn : 0m))).ToList();

        var now = timeProvider.GetUtcNow();
        var created = Consignment.Create(request.OrderId, request.OrderNumber, request.Method, request.DestinationCity, request.District,
            request.PickupPointId, request.RecipientPhone, plans, now);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var consignment = created.Value;
        consignments.Add(consignment);
        var dropoff = request.PickupPointId is null ? null : options.Pickup.Points.Find(p => p.Id == request.PickupPointId)?.Address;

        for (var i = 0; i < consignment.Parcels.Count; i++)
        {
            var parcel = consignment.Parcels[i];
            if (legs[i].IsFailure)
            {
                consignment.RecordBookingRefused(parcel.Id, legs[i].Error.Message, now);
                continue;
            }

            var gateway = couriers.For(parcel.Courier);
            if (gateway.IsFailure)
            {
                consignment.RecordBookingRefused(parcel.Id, gateway.Error.Message, now);
                continue;
            }

            var booking = await gateway.Value.BookAsync(new CourierBookingRequest(parcel.Reference, parcel.OriginCity, request.DestinationCity,
                request.District, dropoff, request.RecipientPhone, parcel.WeightKg, parcel.Vehicle, parcel.CashToCollect,
                new Uri(settings.WebhookBaseAddress, $"api/shipping/webhooks/{parcel.Courier.ToLowerInvariant()}")), cancellationToken);

            if (booking.IsSuccess)
            {
                consignment.RecordBooking(parcel.Id, booking.Value.TrackingCode, now);
            }
            else if (booking.Error is CourierError { IsTransient: true })
            {
                // Nothing is committed: the outbox retries the whole command, and the courier recognises the references.
                throw new InvalidOperationException($"Courier {parcel.Courier} is unavailable: {booking.Error.Message}");
            }
            else
            {
                consignment.RecordBookingRefused(parcel.Id, booking.Error.Message, now);
            }
        }

        return Result.Success();
    }

    public async ValueTask<Result> Handle(HandleCourierWebhook request, CancellationToken cancellationToken)
    {
        var gateway = couriers.For(request.Courier);
        if (gateway.IsFailure)
        {
            return gateway.Error;
        }

        var update = gateway.Value.ParseWebhook(request.Body, request.Signature);
        if (update.IsFailure)
        {
            return update.Error;
        }

        var consignment = await consignments.FindByParcelReferenceAsync(update.Value.Reference, cancellationToken);
        var parcel = consignment?.FindParcel(update.Value.Reference);
        if (consignment is null || parcel is null)
        {
            return Error.NotFound("shipping.parcel_not_found", $"No parcel {update.Value.Reference}.");
        }

        if (consignment.Track(parcel.Id, update.Value.Status, update.Value.Detail, timeProvider.GetUtcNow()).IsFailure)
        {
            // Couriers resend webhooks and can deliver them out of order: acknowledge, keep the newer state.
            LogIgnoredUpdate(logger, update.Value.Reference, parcel.Status, update.Value.Status);
        }

        return Result.Success();
    }

    public async ValueTask<Result<ConsignmentDto>> Handle(CollectConsignment request, CancellationToken cancellationToken)
    {
        var consignment = await consignments.GetAsync(request.ConsignmentId, cancellationToken);
        if (consignment is null)
        {
            return Error.NotFound("shipping.consignment_not_found", $"No consignment {request.ConsignmentId}.");
        }

        var collected = consignment.Collect(timeProvider.GetUtcNow());
        return collected.IsSuccess ? ConsignmentDto.From(consignment) : collected.Error;
    }

    public async ValueTask<Result<ConsignmentDto>> Handle(GetConsignmentForOrder request, CancellationToken cancellationToken)
    {
        var consignment = await consignments.FindByOrderAsync(request.OrderId, cancellationToken);
        return consignment is null
            ? Error.NotFound("shipping.consignment_not_found", $"Order {request.OrderId} has not been dispatched.")
            : ConsignmentDto.From(consignment);
    }

    public async ValueTask<Result<IReadOnlyList<ShippingOptionDto>>> Handle(GetShippingOptions request, CancellationToken cancellationToken)
    {
        var cart = await carts.GetForCheckoutAsync(request.CartId, cancellationToken);
        if (cart.IsFailure)
        {
            return cart.Error;
        }

        var items = await catalog.GetSellableItemsAsync([.. cart.Value.Lines.Select(l => l.ProductId)], cancellationToken);
        var quantities = cart.Value.Lines.ToDictionary(l => l.ProductId, l => l.Quantity);
        var sellers = (await catalog.GetSellersAsync([.. items.Select(i => i.SellerId).Distinct()], cancellationToken)).ToDictionary(s => s.SellerId);
        var parcels = items.GroupBy(i => i.SellerId)
            .Where(g => sellers.ContainsKey(g.Key))
            .Select(g => new ParcelRequest(g.Key, sellers[g.Key].City, g.Sum(i => i.WeightKg * quantities[i.ProductId])))
            .ToList();

        var available = await quoter.OptionsAsync(new ShippingQuoteRequest(cart.Value.DeliveryCity, request.District, null, request.PickupPointId, parcels), cancellationToken);
        return available.Select(ShippingOptionDto.From).ToList();
    }

    public ValueTask<Result<IReadOnlyList<PickupPointDto>>> Handle(ListPickupPoints request, CancellationToken cancellationToken)
    {
        IReadOnlyList<PickupPointDto> points = [.. options.Pickup.Points
            .Where(p => request.City is null || string.Equals(p.City, request.City, StringComparison.OrdinalIgnoreCase))
            .Select(p => new PickupPointDto(p.Id, p.Name, p.City, p.Address, p.MaxKg))];
        return ValueTask.FromResult(Result.Success(points));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignored courier update for parcel {Reference}: it is {Current}, update says {Update}")]
    private static partial void LogIgnoredUpdate(ILogger logger, string reference, ParcelStatus current, ParcelStatus update);
}
