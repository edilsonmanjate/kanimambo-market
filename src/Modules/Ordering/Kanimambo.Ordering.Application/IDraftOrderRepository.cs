using Kanimambo.Ordering.Domain;

namespace Kanimambo.Ordering.Application;

public interface IDraftOrderRepository
{
    void Add(DraftOrder draft);

    Task<DraftOrder?> GetAsync(Guid id, CancellationToken cancellationToken);
}
