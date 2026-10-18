using Kanimambo.Invoicing.Domain;

namespace Kanimambo.Invoicing.Application;

public interface IFiscalDocumentPdfRenderer
{
    ExportedFile Render(FiscalDocument document);
}
