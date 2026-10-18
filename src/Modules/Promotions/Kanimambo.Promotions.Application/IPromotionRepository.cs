using Kanimambo.Promotions.Domain;

namespace Kanimambo.Promotions.Application;

public interface IPromotionRepository
{
    void Add(Promotion promotion);

    Task<Promotion?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Promotion>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
}
