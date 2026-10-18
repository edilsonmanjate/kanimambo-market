using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Ordering.Application.Checkout;

/// <summary>
/// Facade: turning a cart (or a B2B draft) into an order touches seven subsystems — the cart, catalog prices
/// and sellers, exchange rates, promotions, shipping quotes (Shipping module), the acceptance chain and the order
/// itself, plus payment and idempotency. Callers see one operation.
/// </summary>
public interface ICheckoutService
{
    Task<Result<CheckoutResult>> CheckoutCartAsync(Guid cartId, CheckoutDetails details, CancellationToken cancellationToken);

    Task<Result<CheckoutResult>> SubmitDraftAsync(Guid draftId, CheckoutDetails details, CancellationToken cancellationToken);
}
