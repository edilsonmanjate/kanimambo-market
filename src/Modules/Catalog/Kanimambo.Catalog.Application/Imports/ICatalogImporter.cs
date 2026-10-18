using Kanimambo.Catalog.Domain;

namespace Kanimambo.Catalog.Application.Imports;

public interface ICatalogImporter
{
    ImportFormat Format { get; }

    Task<ImportReport> ImportAsync(SellerId sellerId, ImportSource source, CancellationToken cancellationToken);
}
