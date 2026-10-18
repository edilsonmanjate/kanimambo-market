using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Invoicing.Domain;

/// <summary>An issued invoice or credit note. Immutable once created; all amounts in MZN, IVA included in the gross.</summary>
public abstract class FiscalDocument : AggregateRoot<Guid>
{
    private readonly List<FiscalLine> _lines = [];

    protected FiscalDocument()
    {
    }

    protected FiscalDocument(
        DocumentType type, FiscalRegimeKit kit, int number, DateOnly issueDate, FiscalParty issuer, FiscalParty buyer,
        Guid orderId, long orderNumber, Guid? sellerId, string customerPhone, IEnumerable<FiscalLine> lines, DateTimeOffset now)
        : base(Guid.NewGuid())
    {
        ArgumentNullException.ThrowIfNull(kit);
        Type = type;
        Regime = kit.Regime;
        Series = kit.Series.For(type);
        Year = issueDate.Year;
        Number = number;
        FullNumber = kit.Formatter.FormatNumber(Series, Year, number);
        Title = kit.Formatter.Title(type);
        LegalNotice = kit.Formatter.LegalNotice;
        IssueDate = issueDate;
        // Own copies: parties are stored with each document (and EF cannot share one owned instance between owners).
        Issuer = issuer with { };
        IssuerNuit = issuer.Nuit!;
        Buyer = buyer with { };
        OrderId = orderId;
        OrderNumber = orderNumber;
        SellerId = sellerId;
        CustomerPhone = customerPhone;
        _lines.AddRange(lines.Select(l => l with { })); // a credit note copies the invoice's lines, it must not take them
        Net = _lines.Sum(l => l.Net);
        Iva = _lines.Sum(l => l.Iva);
        Gross = _lines.Sum(l => l.Gross);
        CreatedAt = now;
        Raise(new FiscalDocumentIssued(Id, type, FullNumber, orderId, orderNumber, issuer.Nuit!, issuer.Name, Gross, customerPhone, now));
    }

    public DocumentType Type { get; private set; }

    public FiscalRegime Regime { get; private set; }

    public string Series { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int Number { get; private set; }

    /// <summary>As printed: <c>FT 2026/000123</c>.</summary>
    public string FullNumber { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? LegalNotice { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public FiscalParty Issuer { get; private set; } = null!;

    /// <summary>Copy of the issuer's NUIT as a column of its own: (issuer, series, year, number) is unique.</summary>
    public string IssuerNuit { get; private set; } = string.Empty;

    public FiscalParty Buyer { get; private set; } = null!;

    public Guid OrderId { get; private set; }

    public long OrderNumber { get; private set; }

    /// <summary>The seller that issued the document; null for the marketplace's own documents (delivery).</summary>
    public Guid? SellerId { get; private set; }

    public string CustomerPhone { get; private set; } = string.Empty;

    public decimal Net { get; private set; }

    public decimal Iva { get; private set; }

    public decimal Gross { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<FiscalLine> Lines => _lines.AsReadOnly();

    /// <summary>Double dispatch: the document tells the visitor what it is, then walks its lines.</summary>
    public abstract void Accept(IFiscalDocumentVisitor visitor);

    protected void AcceptLines(IFiscalDocumentVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        foreach (var line in _lines)
        {
            visitor.VisitLine(this, line);
        }

        visitor.LeaveDocument(this);
    }
}
