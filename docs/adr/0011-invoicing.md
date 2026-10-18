# 0011 – Invoicing: prices include IVA, sellers issue their own invoices, issuing never blocks

- **Status:** accepted
- **Date:** 2026-10-18

## Context

Paid orders must produce fiscal documents that add up to what the customer paid. Several facts shape
how:

- **Many sellers per order.** A marketplace order can contain goods from several sellers, each with its
  own NUIT and fiscal regime (IVA normal or ISPC). Delivery is a service sold by the marketplace itself.
- **Two pricing rules in the code.** The Catalog (Phase 1) showed IVA *on top of* the price, while
  checkout (Phase 3) charged the price as it was.
- **Missing fiscal data.** A seller may not have registered its fiscal data yet when an order is paid.
- **Gapless numbering.** Invoice numbers must be sequential, per issuer, series and year.

## Decision

- **Prices include IVA** (the product owner's choice, as usual in Mozambican retail). The price is what
  the customer pays: net = gross / (1 + rate), and IVA = gross − net, rounded per line. The Catalog's
  IVA breakdown was changed to this rule, and checkout totals were unchanged.
- **Who issues what.** Each seller issues an invoice for its own goods, as issuer, in its regime's series
  (Abstract Factory). The marketplace issues the delivery invoice under IVA normal, with its NUIT from
  configuration. The order's promotion discount is spread over all goods lines in proportion, with
  exact cents. The invoices of an order add up exactly to its total.
- **Fiscal data belongs to Invoicing.** `TaxpayerProfile` (NUIT, legal name, regime) is registered with
  `PUT /api/invoicing/taxpayers/{sellerId}`, not stored in the Catalog.
- **Issuing never fails on missing data.** `OrderPaid` creates an `InvoiceRequest` per issuer with a
  snapshot of the sale (lines, tax categories, discount share, buyer):
  - it is issued immediately when possible;
  - it stays `AwaitingTaxpayer` until the seller registers, and is issued in that same call;
  - it becomes `Rejected` (visible in `GET /api/invoicing/requests`) when the data can't form a valid
    invoice.
  The outbox subscriber therefore always succeeds and never blocks other subscribers.
- **Bundles mixing tax categories** (Composite) become one invoice line per category, using the
  Catalog's new `GetTaxProfilesAsync` contract.
- **Refunds.** When an order is refunded (`OrderStatusChanged` → `Refunded`), every issued invoice gets
  a full credit note in its regime's series, and requests not yet issued are cancelled.
- **Gapless numbering.** A `DocumentCounter` row per (issuer, series, year) is incremented in the same
  transaction that saves the document, with optimistic concurrency. The builder takes the number only
  after validation succeeds.
- **The monthly e-Declaração file** (CSV, XLSX, and XML behind a flag) and the IVA summary are Visitors.
  The column layout is ours and must be checked against the AT's current template before filing.

## Consequences

- **Not production-grade fiscal compliance.** It is a model of it: legal mentions, exemption articles
  and series are configuration, and the rates and the legal reference for exemptions should be
  validated by an accountant before real use.
- **Catalog data can drift.** Tax categories are read from the Catalog when the order is paid. A
  category changed between checkout and payment applies the new one; the request snapshot freezes it
  from then on.
- **Credit notes are full only.** Partial credits (returning one item) are a later extension to
  `CreditNote`.
- **The issuer's address and legal name are copied into each document.** Later profile changes don't
  alter documents already issued.
