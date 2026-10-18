using System.Text;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

/// <summary>Monthly file as CSV (semicolon separated, one row per document and tax rate).</summary>
internal sealed class EDeclaracaoCsvVisitor(StringBuilder output) : DeclarationVisitor
{
    public override void VisitPeriod(FiscalPeriod period) =>
        output.Append("NuitEmitente;TipoDocumento;NumeroDocumento;DataEmissao;NuitAdquirente;NomeAdquirente;DocumentoOrigem;CategoriaFiscal;Taxa;BaseTributavel;Iva;Total\n");

    protected override void WriteDocument(FiscalDocument document, IReadOnlyList<TaxBucket> buckets)
    {
        foreach (var bucket in buckets)
        {
            output.Append(document.IssuerNuit).Append(';')
                .Append(TypeCode(document)).Append(';')
                .Append(document.FullNumber).Append(';')
                .Append(document.IssueDate.ToString("yyyy-MM-dd", Invariant)).Append(';')
                .Append(document.Buyer.Nuit ?? "999999999").Append(';')
                .Append(Clean(document.Buyer.Name)).Append(';')
                .Append((document as CreditNote)?.CreditedInvoiceNumber).Append(';')
                .Append(bucket.Category).Append(';')
                .Append(bucket.Rate.ToString("0.00", Invariant)).Append(';')
                .Append(Amount(bucket.Net)).Append(';')
                .Append(Amount(bucket.Iva)).Append(';')
                .Append(Amount(bucket.Gross)).Append('\n');
        }
    }

    private static string Clean(string value) => value.Replace(';', ',').Replace('\n', ' ');
}
