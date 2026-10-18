using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kanimambo.Invoicing.Infrastructure;

/// <summary>The counter row is tracked and saved with the document, so a number is used if and only if the document is saved.</summary>
internal sealed class DocumentNumbering(InvoicingDbContext db) : IDocumentNumbering
{
    public async Task<DocumentCounter> CounterAsync(string issuerNuit, string series, int year, CancellationToken cancellationToken)
    {
        var counter = db.Counters.Local.SingleOrDefault(c => c.IssuerNuit == issuerNuit && c.Series == series && c.Year == year)
            ?? await db.Counters.SingleOrDefaultAsync(c => c.IssuerNuit == issuerNuit && c.Series == series && c.Year == year, cancellationToken);
        if (counter is null)
        {
            counter = new DocumentCounter(issuerNuit, series, year);
            db.Counters.Add(counter);
        }

        return counter;
    }
}
