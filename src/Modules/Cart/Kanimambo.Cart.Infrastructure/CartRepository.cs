using Kanimambo.Cart.Application;
using Kanimambo.Cart.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Cart.Infrastructure;

internal sealed class CartRepository(CartDbContext db) : ICartRepository
{
    public Task<ShoppingCart?> GetAsync(CartId id, CancellationToken cancellationToken) =>
        db.Carts.Include(c => c.Lines).SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Add(ShoppingCart cart) => db.Carts.Add(cart);
}
