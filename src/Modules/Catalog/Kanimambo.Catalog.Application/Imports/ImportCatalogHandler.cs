using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Domain;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Imports;

public sealed class ImportCatalogHandler(ISellerRepository sellers, IEnumerable<ICatalogImporter> importers)
    : IRequestHandler<ImportCatalog, Result<ImportReport>>
{
    public async ValueTask<Result<ImportReport>> Handle(ImportCatalog request, CancellationToken cancellationToken)
    {
        var seller = await sellers.GetAsync(new SellerId(request.SellerId), cancellationToken);
        if (seller is null)
        {
            return CatalogErrors.SellerNotFound(request.SellerId);
        }

        if (!seller.IsActive)
        {
            return CatalogErrors.SellerSuspended(seller.Name);
        }

        var importer = importers.SingleOrDefault(i => i.Format == request.Format);
        if (importer is null)
        {
            return Error.Validation(new FieldError("format", $"Format '{request.Format}' is not supported."));
        }

        var report = await importer.ImportAsync(seller.Id, new ImportSource(request.SourceName, request.Content), cancellationToken);

        return report.Succeeded
            ? report
            : Error.BusinessRule("catalog.import_unreadable", $"The import could not be read: {report.FatalError}");
    }
}
