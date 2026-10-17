# Proxy

## Problem

Importers price in USD or ZAR, while customers see MZN, so every search and every product page needs
today's rate. The exchange-rate API is **slow** (about 800 ms) and has a **call quota**: it answers
`429 Too Many Requests` when exceeded. A rate is valid for the whole day, so asking for it on every
request wastes latency and burns the quota.

## Naive solution

```csharp
var rate = await http.GetFromJsonAsync<Rate>($"api/rates/USD/MZN?date={today}");  // on every page view
```

Why it hurts:

- every page view pays the API's latency;
- the quota runs out during traffic spikes;
- fifty concurrent requests at the start of the day make fifty identical calls.

## Solution

`CachingExchangeRateProxy` implements the same `IExchangeRateProvider` interface as the real subject,
`HttpExchangeRateClient`. The application only ever receives the proxy and can't tell the two apart.
The proxy controls access to the expensive subject in three ways:

1. **Cache per (pair, Maputo day)** in Redis, shared by all API instances. A rate expires at the next
   local midnight (CAT, UTC+2); past dates are kept for 7 days.
2. **Single flight.** Concurrent misses for the same key share one in-flight call (`Lazy<Task>` per
   key).
3. **Rate limit.** A token bucket keeps calls under the upstream quota. Over the limit, the proxy
   answers `Unavailable` (503) instead of hammering the API.

Failures are **not cached**, so the next request retries. Converting a currency to itself never calls
upstream.

```mermaid
sequenceDiagram
    participant H as Handler (search / details)
    participant P as CachingExchangeRateProxy
    participant R as Redis
    participant S as HttpExchangeRateClient
    participant A as Rates API (simulator)
    H->>P: GetRate(USD, MZN, today)
    P->>R: GET fx:USD:MZN:2026-10-17
    alt cached
        R-->>P: 63.97
    else miss (first caller; others await the same task)
        P->>P: token bucket: permit?
        P->>S: GetRate(...)
        S->>A: GET /api/rates/USD/MZN?date=...
        A-->>S: 63.97
        P->>R: SET (expires at Maputo midnight)
    end
    P-->>H: 63.97
```

**Proxy or Decorator?** The structure is the same. The intent differs: this class controls *access* to
one expensive resource (whether, when and how often it is called). The resilience
Decorators of Phase 4 add behaviour around any client without changing when it is
called.

## Where in the code

- Port:
  [ExchangeRates.cs](../../src/Modules/Catalog/Kanimambo.Catalog.Application/Abstractions/ExchangeRates.cs)
- Proxy:
  [CachingExchangeRateProxy.cs](../../src/Modules/Catalog/Kanimambo.Catalog.Infrastructure/ExchangeRates/CachingExchangeRateProxy.cs);
  subject:
  [HttpExchangeRateClient.cs](../../src/Modules/Catalog/Kanimambo.Catalog.Infrastructure/ExchangeRates/HttpExchangeRateClient.cs)
- Wiring (the only `IExchangeRateProvider` registered is the proxy):
  [CatalogInfrastructureExtensions.cs](../../src/Modules/Catalog/Kanimambo.Catalog.Infrastructure/CatalogInfrastructureExtensions.cs)
- Local API:
  [Kanimambo.Simulators.ExchangeRates](../../src/Simulators/Kanimambo.Simulators.ExchangeRates)
  (simulated rates, 800 ms latency, 30 calls/min)

## Tests

- [CachingExchangeRateProxyTests](../../tests/Kanimambo.Catalog.UnitTests/Proxy/CachingExchangeRateProxyTests.cs)
  cover:
  - **100 requests on the same day → 1 upstream call**;
  - 50 concurrent misses → 1 call;
  - a separate fetch per day and pair;
  - no call for MZN → MZN;
  - failures not cached;
  - over the quota → `exchange_rate.quota_exhausted` without calling upstream;
  - a second proxy instance served from the shared cache.
- [SimulatorContractTests](../../tests/Kanimambo.Catalog.IntegrationTests/SimulatorContractTests.cs)
  run the subject against the simulator.
- [CatalogApiTests](../../tests/Kanimambo.Catalog.IntegrationTests/CatalogApiTests.cs) cover USD
  prices shown in MZN.

## Trade-offs & when NOT to use

- **Stale by design.** A day-long cache is right for a "rate of the day"; it's wrong for live FX
  trading.
- **Single flight is per process.** Several API instances can each make one call per key, which is
  still bounded by the shared cache and the per-instance token bucket.
- **Not needed for cheap, unmetered dependencies.** A plain client is enough there; add a proxy when
  access needs *control*, not just because the call goes over the network.
