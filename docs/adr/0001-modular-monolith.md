# 0001 – Modular monolith with one PostgreSQL schema per module

- **Status:** accepted
- **Date:** 2026-10-17

## Context

Kanimambo Market has eight business areas: Catalog, Cart, Promotions, Ordering, Payments, Shipping,
Invoicing and Notifications. It is developed by a small team and runs locally with no cloud
dependencies. Microservices would add network failures, distributed transactions and deployment cost
before any of that is needed. A single unstructured project, on the other hand, quickly turns into a
big ball of mud.

## Decision

- **One deployable**, `Kanimambo.Api`, made of modules. Each module has
  `Api / Application / Domain / Infrastructure` projects (Clean Architecture).
- **Modules talk only through public contracts and integration events.** No module references another
  module's Domain, Application, Infrastructure or Api.
- **One PostgreSQL database, one schema per module.** Each module has its own `DbContext`, migrations
  and migrations-history table inside its schema. Host-level tables live in the `platform` schema.
- **Shared HTTP building blocks live in `Kanimambo.SharedKernel.Web`.** This covers
  `ApiControllerBase` and the idempotency attributes. Module `*.Api` projects can't reference the host
  (the host references them), and the SharedKernel must stay free of ASP.NET because every Domain
  project references it.
- **Shared EF plumbing lives in `Kanimambo.SharedKernel.Infrastructure`.** `AddModuleDbContext<T>` registers a
  module's DbContext in its own schema, together with its keyed `IUnitOfWork` and `IInboxStore`, and
  marks it for migration at startup.
- **The rules are enforced by `Kanimambo.ArchitectureTests`.** They discover module assemblies by
  name (`Kanimambo.<Module>.<Layer>`), so each new module is covered automatically.

## Consequences

- A module can later be extracted to its own service: its schema and contracts are already isolated.
- Cross-module reads that would be a simple SQL join must go through a contract or a read model.
  This is intentional friction.
- A single transaction cannot span two modules. Consistency across modules is eventual, through the
  outbox (Phase 3).
