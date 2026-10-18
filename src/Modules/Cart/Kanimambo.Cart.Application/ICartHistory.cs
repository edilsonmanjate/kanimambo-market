using Kanimambo.Cart.Domain;

namespace Kanimambo.Cart.Application;

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
