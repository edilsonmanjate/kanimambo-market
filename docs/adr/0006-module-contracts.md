# 0006 – Cross-module collaboration through `*.Contracts`

- **Status:** accepted
- **Date:** 2026-10-17

## Context

Cart needs live prices and stock from Catalog, exchange rates to MZN, and the best promotion from
Promotions. ADR 0001 forbids referencing another module's internals.

## Decision

- **Each module may publish a `Kanimambo.<Module>.Contracts` project.** It contains only interfaces
  and immutable DTOs, and depends on nothing but the SharedKernel:
  - `Catalog.Contracts`: `ICatalogReader` (sellable items with derived bundle stock/price) and
    `ICurrencyConverter`;
  - `Promotions.Contracts`: `IPromotionEvaluator`, `PromotionContext`, `IAppliedPromotion`, and the
    `NoPromotion` null object.
- **The owning module implements its contracts**, inside its Infrastructure or Application layer, and
  registers them in DI. Consumers call them in-process.
- **The architecture tests enforce it.** A `Contracts` project may not depend on module internals or
  frameworks, and Cart's Application must reference Catalog and Promotions only through `Contracts`.
- **Null objects for "nothing to report" live in the contract** (`NoPromotion`), so every consumer gets
  the same neutral behaviour.

## Consequences

- Each module can evolve its internals freely; only contract changes ripple.
- Calls are synchronous and in-process for now. When an interaction must survive failures or cross a
  transaction boundary, it moves to integration events and the outbox (Phase 3).
- A contract read runs on the provider's own DbContext, inside the caller's request but outside the
  caller's transaction. That is acceptable for reads; writes across modules go through events.
