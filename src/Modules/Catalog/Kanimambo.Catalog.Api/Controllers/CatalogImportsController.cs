using Kanimambo.Catalog.Application.Imports;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Catalog.Api.Controllers;

[Route("api/catalog/sellers/{sellerId:guid}/imports")]
public sealed class CatalogImportsController(IMediator mediator) : ApiControllerBase
{
    public const long MaxUploadBytes = 20 * 1024 * 1024;

    private static readonly Dictionary<string, ImportFormat> FormatsByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = ImportFormat.Csv,
        [".xlsx"] = ImportFormat.Excel,
        [".xml"] = ImportFormat.Xml,
    };

    /// <summary>Uploads a CSV, Excel (.xlsx) or XML catalog; the report lists accepted and rejected rows with reasons.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType<ImportReport>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload(Guid sellerId, IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!FormatsByExtension.TryGetValue(Path.GetExtension(file.FileName), out var format))
        {
            return ProblemFrom(Error.Validation(new FieldError("file", "Supported files: .csv, .xlsx, .xml.")));
        }

        await using var content = file.OpenReadStream();
        return OkOrProblem(await mediator.Send(new ImportCatalog(sellerId, format, file.FileName, content), cancellationToken));
    }

    /// <summary>Pulls the seller's catalog from the wholesaler's paginated API, streaming page by page.</summary>
    [HttpPost("wholesaler")]
    [ProducesResponseType<ImportReport>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportFromWholesaler(Guid sellerId, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new ImportCatalog(sellerId, ImportFormat.WholesalerApi, "wholesaler-api", null), cancellationToken));
}
