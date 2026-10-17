using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;

namespace Kanimambo.Catalog.Infrastructure.ExchangeRates;

public sealed class ExchangeRatesOptions
{
    public const string SectionName = "Catalog:ExchangeRates";

    public Uri BaseAddress { get; set; } = new("https+http://exchange-rates");

    /// <summary>Upstream quota the proxy enforces (the rates API rejects bursts with 429).</summary>
    public int MaxUpstreamCallsPerMinute { get; set; } = 20;
}

internal sealed record ExchangeRateResponse(string Base, string Quote, DateOnly Date, decimal Rate);

/// <summary>
/// The real subject: calls the (slow, rate-limited) exchange-rate API. Locally that API is the
/// exchange-rate simulator. Never used directly by the application — always through the proxy.
/// </summary>
internal sealed class HttpExchangeRateClient(IHttpClientFactory httpClientFactory) : IExchangeRateProvider
{
    public const string HttpClientName = "exchange-rates";

    public async Task<Result<IExchangeRate>> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        using var http = httpClientFactory.CreateClient(HttpClientName);
        var uri = new Uri(string.Create(CultureInfo.InvariantCulture, $"api/rates/{from.Code}/{to.Code}?date={date:yyyy-MM-dd}"), UriKind.Relative);

        try
        {
            using var response = await http.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return Error.Unavailable("exchange_rate.upstream_rate_limited", "The exchange-rate API is rate limiting us.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Error.Unavailable("exchange_rate.upstream_error", $"The exchange-rate API answered {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<ExchangeRateResponse>(cancellationToken);
            return body is { Rate: > 0m }
                ? Result.Success<IExchangeRate>(new ExchangeRate(from, to, date, body.Rate))
                : Error.Unavailable("exchange_rate.upstream_invalid", "The exchange-rate API returned no rate.");
        }
        catch (HttpRequestException exception)
        {
            return Error.Unavailable("exchange_rate.upstream_unreachable", exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.Unavailable("exchange_rate.upstream_timeout", "The exchange-rate API timed out.");
        }
    }
}
