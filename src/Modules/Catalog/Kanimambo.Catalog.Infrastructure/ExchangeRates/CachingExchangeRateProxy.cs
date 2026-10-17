using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.RateLimiting;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Kanimambo.SharedKernel.Time;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Kanimambo.Catalog.Infrastructure.ExchangeRates;

/// <summary>
/// Proxy in front of the exchange-rate API, with the same <see cref="IExchangeRateProvider"/> interface, so
/// callers cannot tell it apart from the real subject. It controls access to an expensive resource:
/// <list type="bullet">
/// <item><b>Cache</b> — a rate is valid for the whole (Maputo) day, so it is fetched once per currency pair
/// per day and kept in Redis (shared by every API instance).</item>
/// <item><b>Single flight</b> — concurrent misses for the same key share one upstream call.</item>
/// <item><b>Rate limit</b> — never exceeds the upstream quota; over the limit it answers "unavailable" instead
/// of hammering the API.</item>
/// </list>
/// </summary>
internal sealed partial class CachingExchangeRateProxy(
    IExchangeRateProvider subject,
    IDistributedCache cache,
    RateLimiter upstreamLimiter,
    TimeProvider timeProvider,
    ILogger<CachingExchangeRateProxy> logger) : IExchangeRateProvider
{
    private static readonly TimeSpan PastRateLifetime = TimeSpan.FromDays(7);

    private readonly ConcurrentDictionary<string, Lazy<Task<Result<IExchangeRate>>>> _inFlight = new();

    public async Task<Result<IExchangeRate>> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (from == to)
        {
            return Result.Success<IExchangeRate>(NoExchangeRate.For(from));
        }

        var key = string.Create(CultureInfo.InvariantCulture, $"fx:{from.Code}:{to.Code}:{date:yyyy-MM-dd}");

        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (decimal.TryParse(cached, NumberStyles.Number, CultureInfo.InvariantCulture, out var cachedRate))
        {
            return Result.Success<IExchangeRate>(new ExchangeRate(from, to, date, cachedRate));
        }

        var fetch = _inFlight.GetOrAdd(key, _ => new Lazy<Task<Result<IExchangeRate>>>(() => FetchAndCacheAsync(from, to, date, key)));
        try
        {
            return await fetch.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (fetch.Value.IsCompleted)
            {
                _inFlight.TryRemove(KeyValuePair.Create(key, fetch));
            }
        }
    }

    // Shared by every waiting caller, so it is not tied to any single caller's cancellation token.
    private async Task<Result<IExchangeRate>> FetchAndCacheAsync(Currency from, Currency to, DateOnly date, string key)
    {
        using var lease = upstreamLimiter.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            LogQuotaExhausted(logger, key);
            return Error.Unavailable("exchange_rate.quota_exhausted", "Exchange-rate quota exhausted; try again shortly.");
        }

        var result = await subject.GetRateAsync(from, to, date, CancellationToken.None);
        if (result.IsFailure)
        {
            LogUpstreamFailed(logger, key, result.Error.Code);
            return result;
        }

        var expiresAt = date < MozambiqueTime.Today(timeProvider)
            ? timeProvider.GetUtcNow() + PastRateLifetime
            : MozambiqueTime.EndOfDay(timeProvider);

        await cache.SetStringAsync(
            key,
            result.Value.Rate.ToString(CultureInfo.InvariantCulture),
            // Relative, so the cache server's clock never has to agree with ours.
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiresAt - timeProvider.GetUtcNow() },
            CancellationToken.None);

        LogFetched(logger, key, result.Value.Rate);
        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Fetched exchange rate {Key} = {Rate} from upstream")]
    private static partial void LogFetched(ILogger logger, string key, decimal rate);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exchange-rate upstream quota exhausted for {Key}")]
    private static partial void LogQuotaExhausted(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exchange-rate upstream failed for {Key}: {ErrorCode}")]
    private static partial void LogUpstreamFailed(ILogger logger, string key, string errorCode);
}
