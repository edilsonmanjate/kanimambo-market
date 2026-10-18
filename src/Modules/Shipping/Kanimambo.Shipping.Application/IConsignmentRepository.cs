using Kanimambo.Shipping.Domain;

namespace Kanimambo.Shipping.Application;

public interface IConsignmentRepository
{
    void Add(Consignment consignment);

    Task<Consignment?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Consignment?> FindByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Consignment?> FindByParcelReferenceAsync(string reference, CancellationToken cancellationToken);
}
