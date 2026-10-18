using Kanimambo.Promotions.Application;
using Kanimambo.Promotions.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Promotions.Infrastructure;

internal sealed class PromotionRepository(PromotionsDbContext db) : IPromotionRepository
{
    public void Add(Promotion promotion) => db.Promotions.Add(promotion);

    public Task<Promotion?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Promotions.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Promotion>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
        await db.Promotions.Where(p => !activeOnly || p.IsActive).OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);
}
