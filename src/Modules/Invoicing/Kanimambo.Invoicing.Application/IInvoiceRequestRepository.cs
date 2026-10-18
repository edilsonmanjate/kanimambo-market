using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application;

public interface IInvoiceRequestRepository
{
    void Add(InvoiceRequest request);

    Task<IReadOnlyList<InvoiceRequest>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvoiceRequest>> ListAwaitingAsync(Guid sellerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvoiceRequest>> ListByStatusAsync(InvoiceRequestStatus status, CancellationToken cancellationToken);
}
