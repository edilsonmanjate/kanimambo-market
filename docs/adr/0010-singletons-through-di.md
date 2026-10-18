# 0010 – Singletons through the DI container, never a static `Instance`

- **Status:** accepted
- **Date:** 2026-10-18

## Context

Some objects must exist once per application:

- the IVA rate table, read on every product page, checkout and invoice line;
- the feature-flag provider;
- object pools and the regime factories.

The classic Singleton (`private` constructor plus `static Instance`) guarantees one instance, but it
also:

- **hides the dependency.** Call sites reach into a global, and constructors don't show what a class
  needs;
- **breaks test isolation.** xUnit runs test classes in parallel, and a static instance is shared by all
  of them, so a test that changes rates affects the others;
- **can't be replaced.** A test or another host can't substitute a fake;
- **fixes its own lifetime.** It is created on first use, with no control over when its dependencies,
  such as configuration, are ready.

## Decision

- **One instance means one DI registration.** It is registered with `AddSingleton` and injected through
  an interface (`ITaxRateTable`, `IFeatureFlags`, `ObjectPoolProvider`, `InvoicingPools`). No class in
  the solution exposes a static instance of a service with dependencies or configuration.
- **Singletons that change are thread-safe by design.** They hold an immutable snapshot (a
  `FrozenDictionary`) and replace it atomically (`Volatile.Write`) when configuration reloads
  (`IOptionsMonitor.OnChange`). Readers never lock and never see half an update. Invalid
  configuration is refused, and the previous snapshot stays.
- **Singletons don't depend on scoped services** (such as a `DbContext`). They take only options and
  other singletons.
- **Other modules use a singleton through its module's contract.** Catalog reads IVA rates through
  `Kanimambo.Invoicing.Contracts.ITaxRateTable`. That replaced the Catalog's own copy of the rates, so
  there is one source of truth with legal history (`ValidFrom`).

## Consequences

- **Tests build their own instances.** They `new TaxRateTable(rates)` or use their own
  `ServiceProvider`, and never interfere with each other.
- **"Exactly one instance" is a container guarantee, and a test proves it** (the same instance from the
  root, from a scope, and through the contract).
- **Static values are not services.** Immutable, stateless values may still be shared as static
  constants. The Null Objects `GuestCustomer.Instance` and `NoPromotion.Instance` have no dependencies,
  no configuration and nothing to replace in a test.
- **The one service-like exception is `VariantAttributesPool.Shared` (the Flyweight, Phase 1).** It
  stays static because EF Core value converters can't receive services. That limitation is documented
  where it applies, and it is a pure cache with no configuration.
