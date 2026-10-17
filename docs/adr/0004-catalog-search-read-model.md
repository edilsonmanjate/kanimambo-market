# 0004 – Catalog search over a recursive SQL read model

- **Status:** accepted
- **Date:** 2026-10-17

## Context

Search filters on price, stock, seller rating and delivery city must run in PostgreSQL
(Specification). Two things make the write model a poor fit for querying:

- **Bundle figures are derived.** The price, weight and stock of a bundle come from its components,
  recursively (Composite).
- **The filters span aggregates.** They need seller attributes, which belong to another aggregate.

## Decision

- A view, `catalog.catalog_entries`, flattens every item and its seller. A **recursive CTE** expands
  bundles to their leaf products, summing quantities per leaf, and derives each total:
  - price is `Σ price × qty`;
  - weight is `Σ weight × qty`;
  - stock is `MIN(stock / qty)`.

  These are the same rules as the domain composite. Suspended sellers are excluded.
- The view is mapped as a keyless EF entity (`CatalogEntry`, an Application read model) and created in
  the module's migration from `CatalogEntriesViewSql`.
- An integration test asserts that the view and the domain agree on the seeded nested bundles.

## Consequences

- Search never loads aggregates, so specifications translate to a single `SELECT … WHERE` over the
  view.
- The rules exist twice, in C# and in SQL. The integration test is the guard; a change to bundle
  arithmetic must touch both.
- The view must be recreated in a migration when the underlying columns change.
- For very large catalogs the view can become a materialized view refreshed on change, without
  touching the specifications.
