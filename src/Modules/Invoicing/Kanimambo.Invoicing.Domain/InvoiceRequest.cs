using Kanimambo.SharedKernel.Domain;

namespace Kanimambo.Invoicing.Domain;

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
