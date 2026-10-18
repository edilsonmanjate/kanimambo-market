using Kanimambo.Invoicing.Application;
using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Infrastructure.Documents;

internal sealed class FiscalDocumentPdfRenderer(InvoicingPools pools) : IFiscalDocumentPdfRenderer
{
    public ExportedFile Render(FiscalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var writer = pools.PdfWriters.Get();
        try
        {
            document.Accept(new InvoicePdfVisitor(writer));
            return new ExportedFile($"{document.FullNumber.Replace(' ', '-').Replace('/', '-')}.pdf", "application/pdf", writer.ToArray());
        }
        finally
        {
            pools.PdfWriters.Return(writer);
        }
    }
}
