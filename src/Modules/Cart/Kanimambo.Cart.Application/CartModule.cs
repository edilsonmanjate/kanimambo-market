using Kanimambo.Cart.Domain;
using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Cart.Application;

public static class CartModule
{
    public const string Name = "Cart";

    /// <summary>How many snapshots are kept per cart; older ones are pruned.</summary>
    public const int SnapshotsKept = 20;
}

public interface ICartRepository
{
    Task<ShoppingCart?> GetAsync(CartId id, CancellationToken cancellationToken);

    void Add(ShoppingCart cart);
}

/// <summary>
/// Memento caretaker: stores and returns cart snapshots without looking inside them. History is a linear
/// stack — going back to a snapshot discards the ones taken after it.
/// </summary>
public interface ICartHistory
{
    void Save(CartId cartId, CartMemento memento);

    /// <summary>Snapshots ordered from oldest to newest.</summary>
    Task<IReadOnlyList<CartMemento>> ListAsync(CartId cartId, CancellationToken cancellationToken);

    Task DiscardAfterAsync(CartId cartId, int version, CancellationToken cancellationToken);

    /// <summary>Keeps only the <paramref name="keepLatest"/> snapshots up to <paramref name="newestVersion"/>.</summary>
    Task PruneAsync(CartId cartId, int newestVersion, int keepLatest, CancellationToken cancellationToken);
}

internal static class CartAccess
{
    /// <summary>Loads a cart the current actor may use. A cart that belongs to someone else is reported as not found.</summary>
    public static async Task<Result<ShoppingCart>> LoadAsync(ICartRepository carts, ICurrentActor actor, Guid cartId, CancellationToken cancellationToken)
    {
        var cart = await carts.GetAsync(new CartId(cartId), cancellationToken);
        return cart is not null && cart.Customer.CanAccess(actor)
            ? cart
            : Error.NotFound("cart.not_found", $"Cart {cartId} does not exist.");
    }
}
