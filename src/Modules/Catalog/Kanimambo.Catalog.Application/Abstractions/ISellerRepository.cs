using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Sellers;

namespace Kanimambo.Catalog.Application.Abstractions;

public interface ISellerRepository
{
    Task<Seller?> GetAsync(SellerId id, CancellationToken cancellationToken);

    void Add(Seller seller);
}
