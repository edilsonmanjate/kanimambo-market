using System.Threading.RateLimiting;
using Kanimambo.Catalog.Application.Abstractions;
using Kanimambo.Catalog.Infrastructure.ExchangeRates;
using Kanimambo.SharedKernel.Domain;
using Kanimambo.SharedKernel.Results;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kanimambo.Catalog.UnitTests.Proxy;

public sealed class CachingExchangeRateProxyTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly CountingRatesApi _api = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.FromHours(2)));
    private readonly MemoryDistributedCache _cache = new(Options.Create(new MemoryDistributedCacheOptions()));
    private readonly TokenBucketRateLimiter _limiter = Limiter(permits: 100);

    [Fact]
    public async Task Many_requests_on_the_same_day_cost_one_upstream_call()
    {
        var proxy = CreateProxy();

        for (var i = 0; i < 100; i++)
        {
            (await proxy.GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct)).Value.Rate.ShouldBe(63.90m);
        }

        _api.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Concurrent_cache_misses_share_a_single_upstream_call()
    {
        _api.Latency = TimeSpan.FromMilliseconds(200);
        var proxy = CreateProxy();

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => proxy.GetRateAsync(Currency.Zar, Currency.Mzn, Today, Ct)));

        results.ShouldAllBe(r => r.IsSuccess && r.Value.Rate == 3.55m);
        _api.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Each_day_and_currency_pair_is_fetched_separately()
    {
        var proxy = CreateProxy();

        await proxy.GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct);
        await proxy.GetRateAsync(Currency.Usd, Currency.Mzn, Today.AddDays(1), Ct);
        await proxy.GetRateAsync(Currency.Zar, Currency.Mzn, Today, Ct);
        await proxy.GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct);

        _api.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task Converting_a_currency_to_itself_never_calls_upstream()
    {
        (await CreateProxy().GetRateAsync(Currency.Mzn, Currency.Mzn, Today, Ct)).Value.ShouldBeSameAs(NoExchangeRate.For(Currency.Mzn));

        _api.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Failures_are_not_cached_so_the_next_request_retries()
    {
        var proxy = CreateProxy();
        _api.FailNext = true;

        (await proxy.GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct)).Error.Type.ShouldBe(ErrorType.Unavailable);
        (await proxy.GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct)).IsSuccess.ShouldBeTrue();

        _api.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Over_the_quota_the_proxy_answers_unavailable_without_calling_upstream()
    {
        using var limiter = Limiter(permits: 1);
        var proxy = CreateProxy(limiter);

        await proxy.GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct);
        var overQuota = await proxy.GetRateAsync(Currency.Zar, Currency.Mzn, Today, Ct);

        overQuota.Error.Code.ShouldBe("exchange_rate.quota_exhausted");
        _api.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task The_cache_is_shared_so_a_new_proxy_instance_does_not_call_upstream_again()
    {
        await CreateProxy().GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct);
        await CreateProxy().GetRateAsync(Currency.Usd, Currency.Mzn, Today, Ct);

        _api.Calls.ShouldBe(1);
    }

    public void Dispose() => _limiter.Dispose();

    private CachingExchangeRateProxy CreateProxy(RateLimiter? limiter = null) =>
        new(_api, _cache, limiter ?? _limiter, _clock, NullLogger<CachingExchangeRateProxy>.Instance);

    private static TokenBucketRateLimiter Limiter(int permits) => new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = permits,
        TokensPerPeriod = permits,
        ReplenishmentPeriod = TimeSpan.FromHours(1),
        QueueLimit = 0,
        AutoReplenishment = false,
    });

    /// <summary>The real subject's stand-in: counts calls and can be made slow or failing.</summary>
    private sealed class CountingRatesApi : IExchangeRateProvider
    {
        private int _calls;

        public int Calls => _calls;

        public TimeSpan Latency { get; set; } = TimeSpan.Zero;

        public bool FailNext { get; set; }

        public async Task<Result<IExchangeRate>> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(Latency, CancellationToken.None);

            if (FailNext)
            {
                FailNext = false;
                return Error.Unavailable("exchange_rate.upstream_timeout", "timeout");
            }

            return Result.Success<IExchangeRate>(new ExchangeRate(from, to, date, from == Currency.Usd ? 63.90m : 3.55m));
        }
    }
}
