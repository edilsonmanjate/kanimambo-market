using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Sellers;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Catalog.Infrastructure.Persistence;

internal sealed class SellerRepository(CatalogDbContext db) : ISellerRepository
{
    public Task<Seller?> GetAsync(SellerId id, CancellationToken cancellationToken) =>
        db.Sellers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(Seller seller) => db.Sellers.Add(seller);
}
