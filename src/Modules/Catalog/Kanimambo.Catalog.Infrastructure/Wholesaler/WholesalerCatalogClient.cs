using System.Globalization;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Kanimambo.Catalog.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Kanimambo.Catalog.Infrastructure.Wholesaler;

public sealed class WholesalerOptions
{
    public const string SectionName = "Catalog:Wholesaler";

    public Uri BaseAddress { get; set; } = new("https+http://wholesaler");

    public int PageSize { get; set; } = 100;
}

internal sealed record WholesalerPage(int Page, int PageSize, int TotalPages, IReadOnlyList<WholesalerProduct> Items, int? NextPage);

/// <summary>
/// Iterator over the wholesaler's paginated API. Only the current page is referenced: when its items are
/// exhausted the next page replaces it, so memory is bounded by one page regardless of catalog size. Pages
/// are requested lazily — a consumer that stops early never triggers the remaining requests.
/// </summary>
internal sealed class WholesalerCatalogClient(HttpClient http, IOptions<WholesalerOptions> options) : IWholesalerCatalog
{
    public async IAsyncEnumerable<WholesalerProduct> StreamProductsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pageSize = options.Value.PageSize;
        int? nextPage = 1;

        while (nextPage is { } current)
        {
            var uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"api/products?page={current}&pageSize={pageSize}"), UriKind.Relative);
            var page = await http.GetFromJsonAsync<WholesalerPage>(uri, cancellationToken)
                ?? throw new HttpRequestException($"The wholesaler returned an empty body for page {current}.");

            foreach (var product in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return product;
            }

            nextPage = page.NextPage;
        }
    }
}
