using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Invoicing.Domain;

/// <summary>A seller's fiscal identity: who issues its invoices, under which regime. Owned by Invoicing, not Catalog.</summary>
public sealed class TaxpayerProfile : AggregateRoot<Guid>
{
    private TaxpayerProfile()
    {
    }

    private TaxpayerProfile(Guid sellerId, string nuit, string legalName, string? address, FiscalRegime regime, DateTimeOffset now)
        : base(sellerId)
    {
        Nuit = nuit;
        LegalName = legalName;
        Address = address;
        Regime = regime;
        UpdatedAt = now;
    }

    public string Nuit { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public FiscalRegime Regime { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public FiscalParty AsIssuer => new(Nuit, LegalName, Address);

    public static Result<TaxpayerProfile> Register(Guid sellerId, string nuit, string legalName, string? address, FiscalRegime regime, DateTimeOffset now) =>
        Validate(nuit, legalName) is { } error ? error : new TaxpayerProfile(sellerId, nuit.Trim(), legalName.Trim(), address?.Trim(), regime, now);

    /// <summary>Changes apply to documents issued from now on; issued documents keep the data they were issued with.</summary>
    public Result Update(string nuit, string legalName, string? address, FiscalRegime regime, DateTimeOffset now)
    {
        if (Validate(nuit, legalName) is { } error)
        {
            return error;
        }

        Nuit = nuit.Trim();
        LegalName = legalName.Trim();
        Address = address?.Trim();
        Regime = regime;
        UpdatedAt = now;
        return Result.Success();
    }

    private static ValidationError? Validate(string nuit, string legalName)
    {
        var errors = new List<FieldError>();
        if (!Domain.Nuit.IsValid(nuit?.Trim()))
        {
            errors.Add(new FieldError("nuit", "A NUIT has exactly 9 digits."));
        }

        if (string.IsNullOrWhiteSpace(legalName))
        {
            errors.Add(new FieldError("legalName", "The legal name printed on invoices is required."));
        }

        return errors.Count == 0 ? null : Error.Validation(errors);
    }
}

public enum InvoiceRequestStatus
{
    /// <summary>The seller has no fiscal profile yet; the invoice is issued as soon as it registers one.</summary>
    AwaitingTaxpayer,
    Issued,

    /// <summary>The data could not make a valid invoice (e.g. a B2B buyer without a NUIT); needs a human.</summary>
    Rejected,

    /// <summary>The order was refunded before the invoice could be issued.</summary>
    Cancelled,
}

/// <summary>
/// "Issue an invoice for this seller's part of this order". Kept until it is issued, so a paid order is never
/// left without its invoice because a seller's fiscal data was missing at the time.
/// </summary>
public sealed class InvoiceRequest : AggregateRoot<Guid>
{
    /// <summary>Requests for the marketplace's own invoice (delivery) use this seller id.</summary>
    public static readonly Guid Marketplace = Guid.Empty;

    private InvoiceRequest()
    {
    }

    private InvoiceRequest(Guid orderId, long orderNumber, Guid sellerId, FiscalParty buyer, bool buyerNuitRequired, string customerPhone,
        IReadOnlyList<LineToPrice> lines, DateTimeOffset now)
        : base(Guid.NewGuid())
    {
        OrderId = orderId;
        OrderNumber = orderNumber;
        SellerId = sellerId;
        Buyer = buyer with { };
        BuyerNuitRequired = buyerNuitRequired;
        CustomerPhone = customerPhone;
        Lines = lines;
        Status = InvoiceRequestStatus.AwaitingTaxpayer;
        CreatedAt = UpdatedAt = now;
    }

    public Guid OrderId { get; private set; }

    public long OrderNumber { get; private set; }

    public Guid SellerId { get; private set; }

    public FiscalParty Buyer { get; private set; } = null!;

    public bool BuyerNuitRequired { get; private set; }

    public string CustomerPhone { get; private set; } = string.Empty;

    public IReadOnlyList<LineToPrice> Lines { get; private set; } = [];

    public InvoiceRequestStatus Status { get; private set; }

    public Guid? InvoiceId { get; private set; }

    public string? Problem { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static InvoiceRequest Create(Guid orderId, long orderNumber, Guid sellerId, FiscalParty buyer, bool buyerNuitRequired, string customerPhone,
        IReadOnlyList<LineToPrice> lines, DateTimeOffset now) =>
        new(orderId, orderNumber, sellerId, buyer, buyerNuitRequired, customerPhone, lines, now);

    public void MarkIssued(Guid invoiceId, DateTimeOffset now)
    {
        InvoiceId = invoiceId;
        Status = InvoiceRequestStatus.Issued;
        Problem = null;
        UpdatedAt = now;
    }

    public void MarkRejected(string problem, DateTimeOffset now)
    {
        Status = InvoiceRequestStatus.Rejected;
        Problem = problem;
        UpdatedAt = now;
    }

    public void MarkAwaitingTaxpayer(DateTimeOffset now)
    {
        Status = InvoiceRequestStatus.AwaitingTaxpayer;
        Problem = "The seller has no fiscal profile (NUIT, regime) yet.";
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        Status = InvoiceRequestStatus.Cancelled;
        UpdatedAt = now;
    }
}

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
