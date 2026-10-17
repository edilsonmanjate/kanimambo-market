using Microsoft.AspNetCore.Mvc;

namespace Kanimambo.Simulators.Wholesaler;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(WholesalerCatalogData catalog, WholesalerSimulatorOptions options, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > options.MaxPageSize)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: $"page >= 1 and 1 <= pageSize <= {options.MaxPageSize}.");
        }

        await Task.Delay(TimeSpan.FromMilliseconds(options.LatencyMillisecondsPerPage), timeProvider, cancellationToken);
        return Ok(catalog.GetPage(page, pageSize));
    }
}
