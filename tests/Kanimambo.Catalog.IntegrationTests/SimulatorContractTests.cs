using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Infrastructure.ExchangeRates;
using Kanimambo.SharedKernel.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Kanimambo.Catalog.IntegrationTests;

/// <summary>The production HTTP clients against the local simulators: both sides agree on the contract.</summary>
public sealed class SimulatorContractTests(CatalogApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_exchange_rate_client_reads_the_simulators_daily_rate()
    {
        var client = factory.Services.GetRequiredService<HttpExchangeRateClient>();
        var date = new DateOnly(2026, 10, 17);

        var first = await client.GetRateAsync(Currency.Usd, Currency.Mzn, date, Ct);
        var second = await client.GetRateAsync(Currency.Usd, Currency.Mzn, date, Ct);

        first.Value.Rate.ShouldBeInRange(63.5m, 64.3m);
        second.Value.Rate.ShouldBe(first.Value.Rate);
        (await client.GetRateAsync(Currency.Zar, Currency.Mzn, date, Ct)).Value.Rate.ShouldBeInRange(3.5m, 3.6m);
    }

    [Fact]
    public async Task The_wholesaler_iterator_streams_the_simulators_whole_catalog()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var wholesaler = scope.ServiceProvider.GetRequiredService<IWholesalerCatalog>();

        var codes = new HashSet<string>();
        await foreach (var product in wholesaler.StreamProductsAsync(Ct))
        {
            codes.Add(product.Code);
        }

        codes.Count.ShouldBe(2_400);
    }
}
