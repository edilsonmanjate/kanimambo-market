using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Contracts;

public interface ICartReader
{
    /// <summary>The cart, if the current actor may use it (same access rule as the Cart API).</summary>
    Task<Result<CartForCheckout>> GetForCheckoutAsync(Guid cartId, CancellationToken cancellationToken);
}
