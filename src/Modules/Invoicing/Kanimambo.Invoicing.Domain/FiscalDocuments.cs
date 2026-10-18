using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Domain;

public sealed record FiscalDocumentIssued(
    Guid DocumentId, DocumentType Type, string Number, Guid OrderId, long OrderNumber, string IssuerNuit, string IssuerName, decimal TotalMzn,
    string CustomerPhone, DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

/// <summary>
/// Visitor over the fiscal document tree (period → documents → lines). Each output — the monthly e-Declaração file
/// in CSV, XLSX or XML, the invoice PDF, the IVA summary, and later SAF-T — is one visitor; the documents never change.
/// </summary>
public interface IFiscalDocumentVisitor
{
    void VisitPeriod(FiscalPeriod period)
    {
    }

    void VisitInvoice(Invoice invoice);

    void VisitCreditNote(CreditNote creditNote);

    void VisitLine(FiscalDocument document, FiscalLine line);

    void LeaveDocument(FiscalDocument document)
    {
    }

    void LeavePeriod(FiscalPeriod period)
    {
    }
}

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

/// <summary>A fatura. Only <see cref="InvoiceBuilder"/> creates invoices, and only valid ones.</summary>
public sealed class Invoice : FiscalDocument
{
    private Invoice()
    {
    }

    internal Invoice(FiscalRegimeKit kit, int number, DateOnly issueDate, FiscalParty issuer, FiscalParty buyer, Guid orderId, long orderNumber,
        Guid? sellerId, string customerPhone, IEnumerable<FiscalLine> lines, DateTimeOffset now)
        : base(DocumentType.Invoice, kit, number, issueDate, issuer, buyer, orderId, orderNumber, sellerId, customerPhone, lines, now)
    {
    }

    public override void Accept(IFiscalDocumentVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.VisitInvoice(this);
        AcceptLines(visitor);
    }
}

/// <summary>A nota de crédito: cancels (here, fully) an invoice, for example when an order is refunded.</summary>
public sealed class CreditNote : FiscalDocument
{
    private CreditNote()
    {
    }

    private CreditNote(FiscalRegimeKit kit, int number, DateOnly issueDate, Invoice invoice, string reason, DateTimeOffset now)
        : base(DocumentType.CreditNote, kit, number, issueDate, invoice.Issuer, invoice.Buyer, invoice.OrderId, invoice.OrderNumber,
            invoice.SellerId, invoice.CustomerPhone, invoice.Lines, now)
    {
        CreditedInvoiceId = invoice.Id;
        CreditedInvoiceNumber = invoice.FullNumber;
        Reason = reason;
    }

    public Guid CreditedInvoiceId { get; private set; }

    public string CreditedInvoiceNumber { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    /// <summary>A credit note for the whole invoice, in the series of the invoice's own regime (number taken only when valid).</summary>
    public static Result<CreditNote> ForRefund(Invoice invoice, FiscalRegimeKit kit, Func<int> nextNumber, DateOnly issueDate, string reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(kit);
        if (kit.Regime != invoice.Regime)
        {
            return Error.BusinessRule("invoicing.regime_mismatch", $"Invoice {invoice.FullNumber} is {invoice.Regime}; it cannot be credited with a {kit.Regime} kit.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation(new FieldError("reason", "A credit note needs a reason."));
        }

        ArgumentNullException.ThrowIfNull(nextNumber);
        return new CreditNote(kit, nextNumber(), issueDate, invoice, reason.Trim(), now);
    }

    public override void Accept(IFiscalDocumentVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.VisitCreditNote(this);
        AcceptLines(visitor);
    }
}

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
