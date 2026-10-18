using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application;

public interface IFiscalDocumentRepository
{
    void Add(FiscalDocument document);

    Task<FiscalDocument?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FiscalDocument>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<bool> IsCreditedAsync(Guid invoiceId, CancellationToken cancellationToken);

    /// <summary>Invoices and credit notes the issuer issued in the month.</summary>
    Task<IReadOnlyList<FiscalDocument>> ListForPeriodAsync(string issuerNuit, int year, int month, CancellationToken cancellationToken);
}
