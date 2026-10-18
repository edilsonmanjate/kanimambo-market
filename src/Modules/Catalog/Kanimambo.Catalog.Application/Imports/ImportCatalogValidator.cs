using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Application.Imports;

public sealed class ImportCatalogValidator : IValidator<ImportCatalog>
{
    public IEnumerable<FieldError> Validate(ImportCatalog request)
    {
        if (request.Format != ImportFormat.WholesalerApi && request.Content is null)
        {
            yield return new FieldError("file", "A file is required for this import format.");
        }
    }
}
