using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.Catalog.Domain.Sellers;

namespace Kanimambo.Catalog.UnitTests;

internal sealed class InMemorySellers : ISellerRepository
{
    private readonly Dictionary<SellerId, Seller> _sellers = [];

    public InMemorySellers(params Seller[] sellers)
    {
        foreach (var seller in sellers)
        {
            Add(seller);
        }
    }

    public Task<Seller?> GetAsync(SellerId id, CancellationToken cancellationToken) => Task.FromResult(_sellers.GetValueOrDefault(id));

    public void Add(Seller seller) => _sellers[seller.Id] = seller;
}
