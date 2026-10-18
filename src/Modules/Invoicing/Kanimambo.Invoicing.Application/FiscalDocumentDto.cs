using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application;

public sealed record FiscalDocumentDto(
    Guid Id,
    string Type,
    string Title,
    string Number,
    string Regime,
    DateOnly IssueDate,
    string IssuerNuit,
    string IssuerName,
    string? BuyerNuit,
    string BuyerName,
    Guid OrderId,
    long OrderNumber,
    Guid? SellerId,
    decimal Net,
    decimal Iva,
    decimal Gross,
    string? LegalNotice,
    string? CreditedInvoiceNumber,
    IReadOnlyList<FiscalLineDto> Lines)
{
    public static FiscalDocumentDto From(FiscalDocument d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return new(d.Id, d.Type.ToString(), d.Title, d.FullNumber, d.Regime.ToString(), d.IssueDate, d.Issuer.Nuit!, d.Issuer.Name, d.Buyer.Nuit, d.Buyer.Name,
            d.OrderId, d.OrderNumber, d.SellerId, d.Net, d.Iva, d.Gross, d.LegalNotice, (d as CreditNote)?.CreditedInvoiceNumber,
            [.. d.Lines.Select(l => new FiscalLineDto(l.LineNumber, l.Description, l.Sku, l.Quantity, l.TaxCategory, l.Rate, l.Discount, l.Net, l.Iva, l.Gross, l.ExemptionReason))]);
    }
}
