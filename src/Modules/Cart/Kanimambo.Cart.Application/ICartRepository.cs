using Kanimambo.Cart.Domain;

namespace Kanimambo.Cart.Application;

public interface ICartRepository
{
    Task<ShoppingCart?> GetAsync(CartId id, CancellationToken cancellationToken);

    void Add(ShoppingCart cart);
}
