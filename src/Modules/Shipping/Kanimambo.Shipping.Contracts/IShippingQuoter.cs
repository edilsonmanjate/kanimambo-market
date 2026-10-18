using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Shipping.Contracts;

/// <summary>Delivery prices for checkout (synchronous, no side effects).</summary>
public interface IShippingQuoter
{
    /// <summary>The quote for the requested method, or for the first configured method that can serve the order.</summary>
    Task<Result<ShippingQuote>> QuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken);

    /// <summary>Every method that can serve the order, cheapest first (the method in the request is ignored).</summary>
    Task<IReadOnlyList<ShippingQuote>> OptionsAsync(ShippingQuoteRequest request, CancellationToken cancellationToken);
}
