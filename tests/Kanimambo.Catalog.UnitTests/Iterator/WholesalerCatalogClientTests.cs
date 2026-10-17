using System.Net;
using System.Net.Http.Json;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Infrastructure.Wholesaler;
using Microsoft.Extensions.Options;

namespace Kanimambo.Catalog.UnitTests.Iterator;

public sealed class WholesalerCatalogClientTests : IDisposable
{
    private const int PageSize = 100;
    private const int TotalProducts = 1_050;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly PagedWholesaler _api = new(TotalProducts, PageSize);

    [Fact]
    public async Task Pages_are_requested_only_when_the_consumer_reaches_them()
    {
        var consumed = 0;
        await foreach (var _ in CreateClient().StreamProductsAsync(Ct))
        {
            consumed++;
            if (consumed == 5)
            {
                break;
            }
        }

        _api.RequestedPages.ShouldBe([1]);
    }

    [Fact]
    public async Task At_most_one_page_is_buffered_at_any_time()
    {
        await foreach (var product in CreateClient().StreamProductsAsync(Ct))
        {
            // When an item of page N is being consumed, page N + 1 has not been requested yet.
            var pageOfItem = (int.Parse(product.Code.AsSpan(3), System.Globalization.CultureInfo.InvariantCulture) - 1) / PageSize + 1;
            _api.RequestedPages[^1].ShouldBe(pageOfItem);
        }

        _api.RequestedPages.ShouldBe(Enumerable.Range(1, 11));
    }

    [Fact]
    public async Task Streams_the_whole_catalog_in_order()
    {
        var codes = new List<string>();
        await foreach (var product in CreateClient().StreamProductsAsync(Ct))
        {
            codes.Add(product.Code);
        }

        codes.Count.ShouldBe(TotalProducts);
        codes.Distinct().Count().ShouldBe(TotalProducts);
        codes[^1].ShouldBe("WS-01050");
    }

    [Fact]
    public async Task Cancellation_stops_the_stream_and_any_further_requests()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var consumed = 0;

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in CreateClient().StreamProductsAsync(cancellation.Token))
            {
                if (++consumed == 150)
                {
                    await cancellation.CancelAsync();
                }
            }
        });

        consumed.ShouldBe(150);
        _api.RequestedPages.ShouldBe([1, 2]);
    }

    public void Dispose() => _api.Dispose();

    private WholesalerCatalogClient CreateClient() =>
        new(new HttpClient(_api, disposeHandler: false) { BaseAddress = new Uri("http://wholesaler.test/") },
            Options.Create(new WholesalerOptions { PageSize = PageSize }));

    /// <summary>Fake paginated API that records which pages were requested, in order.</summary>
    private sealed class PagedWholesaler(int total, int pageSize) : HttpMessageHandler
    {
        public List<int> RequestedPages { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var page = int.Parse(query["page"]!, System.Globalization.CultureInfo.InvariantCulture);
            RequestedPages.Add(page);

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            var items = Enumerable.Range(((page - 1) * pageSize) + 1, Math.Max(0, Math.Min(pageSize, total - ((page - 1) * pageSize))))
                .Select(i => new WholesalerProduct($"WS-{i:00000}", $"Produto {i}", "mercearia", "SC25KG", 185_000, "MZN", 10, 25_000, "exempt"))
                .ToList();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { page, pageSize, totalPages, items, nextPage = page < totalPages ? page + 1 : (int?)null }),
            });
        }
    }
}
