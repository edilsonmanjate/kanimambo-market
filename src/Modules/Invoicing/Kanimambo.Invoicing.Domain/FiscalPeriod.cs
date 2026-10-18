namespace Kanimambo.Invoicing.Domain;

/// <summary>The documents one issuer issued in a month: the root of the tree the monthly AT file is generated from.</summary>
public sealed class FiscalPeriod(string issuerNuit, string issuerName, int year, int month, IReadOnlyList<FiscalDocument> documents)
{
    public string IssuerNuit { get; } = issuerNuit;

    public string IssuerName { get; } = issuerName;

    public int Year { get; } = year;

    public int Month { get; } = month;

    public IReadOnlyList<FiscalDocument> Documents { get; } = documents;

    public void Accept(IFiscalDocumentVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.VisitPeriod(this);
        foreach (var document in Documents.OrderBy(d => d.IssueDate).ThenBy(d => d.Type).ThenBy(d => d.Number))
        {
            document.Accept(visitor);
        }

        visitor.LeavePeriod(this);
    }
}
