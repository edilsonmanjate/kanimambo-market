using System.Globalization;
using System.Security;
using System.Text;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

/// <summary>Monthly file as XML.</summary>
internal sealed class EDeclaracaoXmlVisitor(StringBuilder output) : DeclarationVisitor
{
    public override void VisitPeriod(FiscalPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        output.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")
            .Append(CultureInfo.InvariantCulture, $"<ComunicacaoFaturas nuitEmitente=\"{period.IssuerNuit}\" nome=\"{Escape(period.IssuerName)}\" ano=\"{period.Year}\" mes=\"{period.Month:D2}\">\n");
    }

    public override void LeavePeriod(FiscalPeriod period) => output.Append("</ComunicacaoFaturas>\n");

    protected override void WriteDocument(FiscalDocument document, IReadOnlyList<TaxBucket> buckets)
    {
        output.Append(Invariant, $"  <Documento tipo=\"{TypeCode(document)}\" numero=\"{Escape(document.FullNumber)}\" data=\"{document.IssueDate:yyyy-MM-dd}\"")
            .Append(Invariant, $" nuitAdquirente=\"{document.Buyer.Nuit ?? "999999999"}\" nomeAdquirente=\"{Escape(document.Buyer.Name)}\" total=\"{Amount(document.Gross)}\"");
        if (document is CreditNote credit)
        {
            output.Append(Invariant, $" documentoOrigem=\"{Escape(credit.CreditedInvoiceNumber)}\"");
        }

        output.Append(">\n");
        foreach (var bucket in buckets)
        {
            output.Append(Invariant, $"    <Imposto categoria=\"{Escape(bucket.Category)}\" taxa=\"{bucket.Rate.ToString("0.00", Invariant)}\" base=\"{Amount(bucket.Net)}\" iva=\"{Amount(bucket.Iva)}\" />\n");
        }

        output.Append("  </Documento>\n");
    }

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
