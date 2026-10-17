# Kanimambo Market

A marketplace backend for Mozambique, built as a **design patterns showcase in .NET 10**.
Each of the 26 patterns below solves a real business problem of the Mozambican market:
M-Pesa / e-Mola / mKesh mobile-money payments, IVA and ISPC tax regimes, monthly invoice
reporting to the Autoridade Tributária (e-Declaração), same-day delivery by *txopela* in Maputo,
and customers on unstable mobile networks.

> **Rule of the project:** no pattern is applied "just because". Every pattern document shows the
> naive solution, why it hurts, and when *not* to use the pattern.

*Kanimambo* means "thank you" in Changana.

## Status

**12 of 26 patterns implemented.**

- **Phase 0 – Foundations:** solution layout, Aspire orchestration, SharedKernel (with the in-house
  **Mediator**), the API host with ProblemDetails and `Idempotency-Key` support, architecture tests and CI.
- **Phase 1 – Catalog:**
  - sellers, products and nested bundles (**Composite**);
  - shared variant attributes (**Flyweight**);
  - SQL-translated search filters (**Specification**);
  - streaming the wholesaler API (**Iterator**);
  - CSV, Excel, XML and API imports (**Template Method**);
  - an audited, undoable back-office (**Command**);
  - cached, rate-limited exchange rates (**Proxy**).
- **Phase 2 – Cart & Promotions:**
  - carts with a snapshot history that survives lost connectivity (**Memento**);
  - pricing without null checks for guests, MZN prices and "no promotion" (**Null Object**);
  - a rule language marketing writes and the system evaluates at runtime (**Interpreter**).

## Design patterns index

Legend: ✅ implemented · 🟡 partially implemented · ⏳ planned

### Behavioral

| Pattern | Business problem | Module | Phase | Status | Docs |
|---|---|---|---|---|---|
| Chain of Responsibility | Order acceptance checks (NUIT, stock, delivery zone, anti-fraud, seller minimum) | Ordering | 3 | ⏳ | — |
| Command | Auditable, undoable back-office operations (bulk price change, suspend seller) | Catalog | 1 | ✅ | [Command](docs/patterns/Command.md) |
| Interpreter | Marketing-authored promotion rules (tokenizer, parser, typed AST) evaluated at runtime | Promotions | 2 | ✅ | [Interpreter](docs/patterns/Interpreter.md) |
| Iterator | Streaming a slow, paginated wholesaler catalog as `IAsyncEnumerable` | Catalog | 1 | ✅ | [Iterator](docs/patterns/Iterator.md) |
| Mediator | In-process dispatch of commands/queries/notifications with ordered pipeline behaviors | SharedKernel | 0 | ✅ | [Mediator](docs/patterns/Mediator.md) |
| Memento | Cart snapshots for customers who lose connectivity: restore snapshot N, undo | Cart | 2 | ✅ | [Memento](docs/patterns/Memento.md) |
| Observer | `OrderPaid` → invoice, SMS, seller notification, stock reservation (with outbox) | Transversal | 3 | ⏳ | — |
| State | Order lifecycle and mobile-money payment lifecycle | Ordering, Payments | 3–4 | ⏳ | — |
| Strategy | Shipping cost: same-day by zone, inter-provincial by weight, pickup point | Shipping | 5 | ⏳ | — |
| Template Method | Seller catalog imports (CSV, Excel, XML, wholesaler API) with fixed steps and per-format hooks | Catalog | 1 | ✅ | [Template Method](docs/patterns/TemplateMethod.md) |
| Visitor | Fiscal document tree → e-Declaração CSV/XLSX/XML, invoice PDF, IVA summary | Invoicing | 6 | ⏳ | — |

### Creational

| Pattern | Business problem | Module | Phase | Status | Docs |
|---|---|---|---|---|---|
| Abstract Factory | Coherent component families per tax regime (IVA normal vs ISPC) | Invoicing | 6 | ⏳ | — |
| Builder | Always-valid `Invoice` (NUITs, series, sequence, exemptions, MZN totals) | Invoicing | 6 | ⏳ | — |
| Factory Method | Payment processor by phone prefix (84/85 M-Pesa, 86/87 e-Mola, 82/83 mKesh) | Payments | 4 | ⏳ | — |
| Null Object | `GuestCustomer`, `NoPromotion`, `NoExchangeRate` remove null checks from cart pricing | Cart, Promotions | 2 | ✅ | [Null Object](docs/patterns/NullObject.md) |
| Object Pool | Reusing buffers for bulk PDF / QR / monthly AT file generation | Invoicing | 6 | ⏳ | — |
| Prototype | Weekly B2B reorders cloned from the previous order | Ordering | 3 | ⏳ | — |
| Singleton | Cached IVA/ISPC rate table and feature flags via DI lifetime | Transversal | 6 | ⏳ | — |

### Structural

| Pattern | Business problem | Module | Phase | Status | Docs |
|---|---|---|---|---|---|
| Adapter | M-Pesa / e-Mola / mKesh (and couriers) behind one gateway with unified errors | Payments, Shipping | 4–5 | ⏳ | — |
| Bridge | Message types × channels (SMS ≤160 chars, WhatsApp, e-mail) | Notifications | 7 | ⏳ | — |
| Composite | Bundles ("cabaz do mês") with recursive price, weight, stock and per-item IVA | Catalog | 1 | ✅ | [Composite](docs/patterns/Composite.md) |
| Decorator | Caching, retry, logging and metrics around repositories and HTTP clients | Infra | 4 | ⏳ | — |
| Facade | `CheckoutService` orchestrating cart, promotions, shipping, order, payment | Ordering | 3 | ⏳ | — |
| Flyweight | 100k product variants sharing interned immutable attributes (−52% retained memory) | Catalog | 1 | ✅ | [Flyweight](docs/patterns/Flyweight.md) |
| Proxy | Cache + rate limit in front of a slow USD/ZAR → MZN exchange-rate API | Catalog | 1 | ✅ | [Proxy](docs/patterns/Proxy.md) |
| Specification | Composable catalog filters translated to SQL by EF Core | Catalog | 0–1 | ✅ | [Specification](docs/patterns/Specification.md) |

## Architecture

A **modular monolith** with Clean Architecture inside each module. Modules communicate only through
public contracts and events, never through each other's `DbContext`
([ADR 0001](docs/adr/0001-modular-monolith.md)).

```mermaid
flowchart LR
    client([Mobile / web clients]) --> api[Kanimambo.Api<br/>controllers · ProblemDetails · Idempotency-Key]
    api --> mediator{{IMediator<br/>Logging → Validation → UnitOfWork → Idempotency}}
    mediator --> catalog[Catalog] & cart[Cart] & promotions[Promotions] & ordering[Ordering]
    mediator --> payments[Payments] & shipping[Shipping] & invoicing[Invoicing] & notifications[Notifications]
    catalog & ordering & payments & invoicing --> pg[(PostgreSQL<br/>one schema per module)]
    api --> redis[(Redis)]
    catalog -.-> fx[[Exchange-rate simulator]]
    catalog -.-> ws[[Wholesaler simulator]]
    payments -.-> mm[[Mobile-money simulator]]
    shipping -.-> courier[[Courier simulator]]
```

```
src/
  Kanimambo.AppHost/                    .NET Aspire orchestration (PostgreSQL, Redis, API, simulators)
  Kanimambo.ServiceDefaults/            OpenTelemetry, health checks, service discovery, resilience
  Kanimambo.Api/                        Host: module registration, ProblemDetails, auth, idempotency middleware
  Kanimambo.SharedKernel/               Entity, AggregateRoot, DomainEvent, Result<T>, Money, Specification, Mediator
  Kanimambo.SharedKernel.Web/           ApiControllerBase (Result → ProblemDetails), shared HTTP attributes
  Kanimambo.SharedKernel.Infrastructure/ EF unit of work, inbox, per-module DbContext registration
  Modules/Catalog/                      {Api, Application, Contracts, Domain, Infrastructure}
  Modules/Promotions/                   {Api, Application, Contracts, Domain, Infrastructure}
  Modules/Cart/                         {Api, Application, Domain, Infrastructure}
  Simulators/                           ExchangeRates, Wholesaler (later: MobileMoney, Courier)
tests/
  Kanimambo.SharedKernel.UnitTests/
  Kanimambo.Catalog.UnitTests/  Kanimambo.Promotions.UnitTests/  Kanimambo.Cart.UnitTests/
  Kanimambo.Api.IntegrationTests/       WebApplicationFactory + Testcontainers (PostgreSQL, Redis)
  Kanimambo.Catalog.IntegrationTests/   real API + in-process simulators
  Kanimambo.Promotions.IntegrationTests/  Kanimambo.Cart.IntegrationTests/
  Kanimambo.ArchitectureTests/          NetArchTest: layer and module isolation rules
  Kanimambo.Benchmarks/                 BenchmarkDotNet (Flyweight; Object Pool in Phase 6)
  Kanimambo.Testing/                    shared test host and JWT helpers
docs/
  patterns/                   one document per pattern
  adr/                        architecture decision records
```

### Cross-cutting behavior

- **Errors.** Business failures travel as `Result`/`Result<T>` with a typed `Error`. They become
  RFC 9457 ProblemDetails with a stable `code` field (for example `ordering.below_seller_minimum`).
  Exceptions are kept for bugs and outages and return a generic 500 response that leaks no details.
- **Idempotency.** Every write endpoint accepts `Idempotency-Key`. A retry gets back the stored first
  response, marked `Idempotent-Replayed: true`. Reusing a key with a different payload returns 422.
  A retry that arrives while the first request is still running returns 409. 5xx responses are not
  stored, so the client can retry. Internal re-deliveries (outbox, payment callbacks) are
  de-duplicated by the Mediator's `IdempotencyBehavior`
  ([ADR 0003](docs/adr/0003-idempotency.md)).
- **Time.** `TimeProvider` is injected everywhere; nothing reads `DateTime.Now`.
- **Money.** `Money` is a `decimal` value object, rounded to 2 decimal places with
  `MidpointRounding.AwayFromZero`. Mixing currencies without an explicit conversion throws.

## Getting started

Prerequisites: [.NET SDK 10.0.402+](https://dotnet.microsoft.com/download) and Docker. The project
needs **no real credentials**: every external gateway runs as a local simulator.

```bash
# Run everything (PostgreSQL, Redis, simulators, API) with the Aspire dashboard.
# In Development the database is migrated and seeded with demo sellers in Maputo, Matola, Beira and Nampula.
dotnet run --project src/Kanimambo.AppHost --launch-profile http

# API reference (Scalar) while the AppHost runs
open http://localhost:5080/scalar

# Build and test (integration tests start their own containers)
dotnet tool restore
dotnet build Kanimambo.slnx
dotnet test --solution Kanimambo.slnx
```

### A short tour of the Catalog

```bash
API=http://localhost:5080

# Specification: groceries in stock, 500–2000 MZN (USD/ZAR prices converted), delivered to Matola, rating ≥ 4
curl "$API/api/catalog/products?inStock=true&category=mercearia&deliversTo=Matola&minSellerRating=4&minPrice=500&maxPrice=2000"

# Composite: "Cabaz Família" (a bundle containing a bundle) with derived stock and IVA per tax category
curl "$API/api/catalog/products/01920000-0000-7000-9000-0000000000c2"

# Template Method + Iterator: import the wholesaler's paginated API into a seller's catalog
curl -X POST "$API/api/catalog/sellers/01920000-0000-7000-8000-000000000002/imports/wholesaler"

# Command: back-office operations need a token with the backoffice role (ADR 0005).
# Run this once, then restart the API so it picks up the signing key:
TOKEN=$(dotnet user-jwts create --project src/Kanimambo.Api --name ana.backoffice --role backoffice --output token)
curl -X POST "$API/api/catalog/backoffice/sellers/01920000-0000-7000-8000-000000000005/price-adjustments" \
     -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"percentage": 8, "reason": "Rand appreciation"}'
curl "$API/api/catalog/backoffice/operations" -H "Authorization: Bearer $TOKEN"     # audit log
curl -X POST "$API/api/catalog/backoffice/operations/<id>/undo" -H "Authorization: Bearer $TOKEN"
```

### A short tour of the Cart and Promotions

```bash
# Memento: a guest cart (keep the id on the device); every change is a snapshot
CART=$(curl -s -X POST "$API/api/carts" -H "Content-Type: application/json" -d '{"deliveryCity":"Maputo"}' | jq -r .id)
curl -X PUT "$API/api/carts/$CART/lines/<productId>" -H "Content-Type: application/json" -d '{"quantity":2}'
curl "$API/api/carts/$CART/snapshots"
curl -X POST "$API/api/carts/$CART/snapshots/1/restore"      # or: POST .../undo

# Null Object + Interpreter: price in MZN with the best promotion (try paymentMethod=MPesa)
curl "$API/api/carts/$CART/pricing?paymentMethod=MPesa"

# Interpreter: check a rule while writing it (back-office token)
curl -X POST "$API/api/promotions/rules/validate" -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" -d '{"rule":"cart.total >= 2500 AN customer.city = \"Maputo\""}'
```

Benchmarks:

```bash
dotnet run -c Release --project tests/Kanimambo.Benchmarks -- --filter "*Flyweight*"
dotnet run -c Release --project tests/Kanimambo.Benchmarks -- --retained
```

Tests run on Microsoft.Testing.Platform (xUnit v3). To collect coverage:

```bash
dotnet test --solution Kanimambo.slnx --coverage --coverage-output-format cobertura --results-directory TestResults
dotnet reportgenerator -reports:"TestResults/*.cobertura.xml" -targetdir:TestResults/report -reporttypes:Html
```

## Roadmap

| Phase | Scope |
|---|---|
| 0 ✅ | Foundations: solution, Aspire, SharedKernel, Mediator, Specification base, API host, CI, architecture tests |
| 1 ✅ | Catalog: Composite, Flyweight, Specification, Iterator, Template Method, Command, Proxy |
| 2 ✅ | Cart & Promotions: Memento, Null Object, Interpreter |
| 3 | Ordering & Checkout: State, Chain of Responsibility, Prototype, Facade, Observer + outbox |
| 4 | Payments: Factory Method, Adapter, payment State, mobile-money simulator, resilience Decorator |
| 5 | Shipping: Strategy, courier Adapter, simulator |
| 6 | Invoicing: Builder, Abstract Factory, Visitor, Object Pool + benchmarks, Singleton |
| 7 | Notifications: Bridge |
| 8 | Polish: C4 diagrams, end-to-end `.http` flow, badges, demo |
