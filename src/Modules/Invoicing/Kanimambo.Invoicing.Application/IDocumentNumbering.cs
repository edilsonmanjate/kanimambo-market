using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application;

/// <summary>Gapless numbering: the counter is tracked by the unit of work and saved with the document.</summary>
public interface IDocumentNumbering
{
    Task<DocumentCounter> CounterAsync(string issuerNuit, string series, int year, CancellationToken cancellationToken);
}
