namespace Kanimambo.Invoicing.Domain;

/// <summary>
/// The last number used in one issuer's series and year. Fiscal numbering must be sequential without gaps, so the
/// counter is incremented in the same transaction that saves the document (optimistic concurrency on the row).
/// </summary>
public sealed class DocumentCounter
{
    private DocumentCounter()
    {
    }

    public DocumentCounter(string issuerNuit, string series, int year)
    {
        IssuerNuit = issuerNuit;
        Series = series;
        Year = year;
    }

    public string IssuerNuit { get; private set; } = string.Empty;

    public string Series { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int LastNumber { get; private set; }

    public int Next() => ++LastNumber;
}
