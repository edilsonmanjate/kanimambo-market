using Kanimambo.Ordering.Domain;

namespace Kanimambo.Ordering.Application;

/// <summary>Caretaker for draft snapshots (same linear-history model as carts).</summary>
public interface IDraftOrderHistory
{
    void Save(Guid draftId, DraftOrderMemento memento);

    Task<IReadOnlyList<DraftOrderMemento>> ListAsync(Guid draftId, CancellationToken cancellationToken);

    Task DiscardAfterAsync(Guid draftId, int version, CancellationToken cancellationToken);
}
