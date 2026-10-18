using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application;

public interface ITaxpayerRepository
{
    void Add(TaxpayerProfile profile);

    Task<TaxpayerProfile?> GetAsync(Guid sellerId, CancellationToken cancellationToken);

    Task<TaxpayerProfile?> FindByNuitAsync(string nuit, CancellationToken cancellationToken);
}
