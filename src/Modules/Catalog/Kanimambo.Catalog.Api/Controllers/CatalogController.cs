using Kanimambo.Catalog.Application.Products;
using Kanimambo.Catalog.Application.Search;
using Kanimambo.SharedKernel.Messaging;
using Kanimambo.SharedKernel.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Catalog.Api.Controllers;

[Route("api/catalog")]
public sealed class CatalogController(IMediator mediator) : ApiControllerBase
{
    /// <summary>
    /// Search with combinable filters, e.g. <c>?inStock=true&amp;minPrice=500&amp;maxPrice=2000&amp;category=mercearia&amp;deliversTo=Matola&amp;minSellerRating=4</c>.
    /// Prices are filtered in MZN, whatever currency the seller uses.
    /// </summary>
    [HttpGet("products")]
    [ProducesResponseType<SharedKernel.Results.PagedResult<CatalogEntryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] CatalogSearchParameters parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var result = await mediator.Send(new SearchCatalog(
            parameters.Q, parameters.InStock, parameters.MinPrice, parameters.MaxPrice, parameters.Category,
            parameters.DeliversTo, parameters.MinSellerRating, parameters.Page, parameters.PageSize), cancellationToken);
        return OkOrProblem(result);
    }

    /// <summary>Product or bundle with its component tree, MZN price, weight, stock and IVA breakdown.</summary>
    [HttpGet("products/{id:guid}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await mediator.Send(new GetProductDetails(id), cancellationToken));

    /// <summary>Creates a bundle ("cabaz") from existing products or bundles of the same seller.</summary>
    [HttpPost("sellers/{sellerId:guid}/bundles")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBundle(Guid sellerId, CreateBundleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await mediator.Send(
            new CreateBundle(sellerId, request.Sku, request.Name, request.Category, request.Components), cancellationToken);
        return CreatedOrProblem(result, nameof(GetProduct), id => new { id });
    }
}
