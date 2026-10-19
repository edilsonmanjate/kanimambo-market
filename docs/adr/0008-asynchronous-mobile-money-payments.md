# 0008 – Asynchronous mobile-money payments, callbacks and a local operator simulator

- **Status:** accepted
- **Date:** 2026-10-18

## Context

In Mozambique most online orders are paid with M-Pesa, e-Mola or mKesh. Collection is asynchronous:

- the merchant asks the operator to charge a phone;
- the operator pushes a PIN prompt to the customer;
- the outcome comes back later in an HTTP callback, or never comes back, if the customer ignores the
  prompt or the callback is lost.

Real sandboxes need contracts and credentials per operator, and the project must run locally without
any.

## Decision

- **A separate Payments module.**
  - It reacts to Ordering's `OrderPaymentRequestedIntegrationEvent`, delivered by the outbox (ADR 0007).
  - It keeps one `Payment` aggregate per (order, attempt), which also makes `StartPayment`
    idempotent.
  - It publishes `PaymentConfirmed` / `PaymentFailed` integration events through its own outbox.
    Ordering applies them; a failure from an older attempt is ignored.
- **Our payment id is the operator reference.**
  - A retried request cannot double-charge, because the operator sees a duplicate reference.
  - Callbacks and status queries are matched by that reference.
- **Callbacks** arrive at `POST /api/payments/callbacks/{operator}` (anonymous):
  - each must carry `X-Signature: sha256=HMAC(body)` with the operator's shared secret;
  - invalid signatures return 401;
  - duplicate or out-of-order callbacks are acknowledged with 204 and ignored by the payment's state.
- **Timeouts.**
  - A payment waiting for a PIN past `Payments:PinTimeout` is checked by a background monitor. The
    monitor first queries the operator, then times the payment out.
  - A confirmation that arrives later still pays the order (late confirmation).
- **Operator routing and resilience:**
  - prefix-based routing (Factory Method);
  - one adapter per operator format (Adapter);
  - logging, metrics, caching and retry around them (Decorator).
  - The operator HttpClients opt out of the generic HTTP resilience handler, so retries happen in one
    business-aware place.
- **`Kanimambo.Simulators.MobileMoney`** stands in for the three operators locally and in tests:
  - it uses three deliberately different, *simulated* wire formats, not the official APIs;
  - the customer confirms after a configurable delay;
  - callbacks are signed and retried until acknowledged;
  - the last four digits of the phone pick the scenario: `0001` insufficient balance, `0002`
    duplicate, `0003` no answer, `0004` delayed callback (after the PIN timeout), `0005` invalid
    number, `0006` operator outage (503), anything else succeeds.
- **Card payments** use a simulated synchronous acquirer: amounts ending in `,99` are declined.
  **Cash on delivery** is never requested; it is paid on delivery (Phase 3).

## Consequences

- **Eventual consistency.** Checkout answers `AwaitingPayment`, and the order becomes `Paid` (or
  `PaymentFailed`) asynchronously, seconds later. Clients poll the order, and the customer gets an SMS
  either way (a confirmation, or a reminder with the reason; see ADR 0012).
- **Configuration and secrets.**
  - Real secrets must come from a vault or user secrets. The callback secrets in `appsettings.json`
    are development placeholders matching the simulator.
  - In Aspire the API's own endpoint is injected as `Payments__CallbackBaseAddress`, so the
    simulator can call back.
- **Connecting a real operator** means one new adapter class and its configuration (prefixes, base
  address, secret). The rest of the flow doesn't change.
- **Reconciliation.** A late confirmation for an order that was cancelled in the meantime is logged
  as needing reconciliation, not applied. A refund flow is out of scope for now.
