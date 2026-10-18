using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.Shipping.Contracts;

namespace Kanimambo.Shipping.Application.Pricing;

/// <summary>
/// Prices an order's delivery. An order has one parcel per seller; every parcel is quoted by the same strategy
/// (the customer chose one method) and the fees add up. The order arrives when its slowest parcel does.
/// </summary>
public sealed class ShippingQuoter(ShippingStrategies strategies, ShippingOptions options) : IShippingQuoter
{
    public Task<Result<ShippingQuote>> QuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Method is { Length: > 0 } method)
        {
            var strategy = strategies.Find(method);
            return Task.FromResult(strategy.IsFailure ? Result<ShippingQuote>.Failure(strategy.Error) : Quote(strategy.Value, request));
        }

        Error? firstRefusal = null;
        foreach (var preferred in options.DefaultMethods)
        {
            var strategy = strategies.Find(preferred);
            if (strategy.IsFailure)
            {
                continue;
            }

            var quote = Quote(strategy.Value, request);
            if (quote.IsSuccess)
            {
                return Task.FromResult(quote);
            }

            firstRefusal ??= quote.Error;
        }

        return Task.FromResult(Result<ShippingQuote>.Failure(firstRefusal
            ?? Error.BusinessRule("shipping.not_served", $"No delivery method can serve {request.DestinationCity}.")));
    }

    public Task<IReadOnlyList<ShippingQuote>> OptionsAsync(ShippingQuoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<ShippingQuote> options = [.. strategies.All
            .Select(s => Quote(s, request))
            .Where(q => q.IsSuccess)
            .Select(q => q.Value)
            .OrderBy(q => q.Fee.Amount)
            .ThenBy(q => q.EstimatedDelivery)];
        return Task.FromResult(options);
    }

    internal static Result<ShippingQuote> Quote(IShippingStrategy strategy, ShippingQuoteRequest request)
    {
        if (request.Parcels.Count == 0)
        {
            return Error.BusinessRule("shipping.no_parcels", "There is nothing to deliver.");
        }

        var parcels = new List<ParcelQuote>(request.Parcels.Count);
        string? pickupPointId = null;
        foreach (var parcel in request.Parcels)
        {
            var leg = strategy.Quote(new ShipmentRoute(parcel.OriginCity, request.DestinationCity, request.District, request.PickupPointId, parcel.WeightKg));
            if (leg.IsFailure)
            {
                return leg.Error;
            }

            pickupPointId ??= leg.Value.PickupPointId;
            parcels.Add(new ParcelQuote(parcel.SellerId, leg.Value.Fee, leg.Value.Courier, leg.Value.Vehicle, leg.Value.EstimatedDelivery));
        }

        var fee = parcels.Select(p => p.Fee).Aggregate(Money.Zero(Currency.Mzn), (a, b) => a + b);
        return new ShippingQuote(strategy.Method, strategy.Description, fee, parcels.Max(p => p.EstimatedDelivery), pickupPointId, parcels);
    }
}
