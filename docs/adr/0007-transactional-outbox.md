# 0007 – Integration events through a transactional outbox per module

- **Status:** accepted
- **Date:** 2026-10-18

## Context

Ordering's events drive work in other modules: stock (Catalog), closing carts (Cart), and later invoices
and SMS. Publishing in-process right after `SaveChanges` loses events on a crash between commit and
publish. Publishing before commit announces changes that may roll back. A message broker would add
infrastructure the project doesn't otherwise need yet.

## Decision

- **Write events in the same transaction.** A module that publishes events registers its DbContext with
  `publishesEvents: true`. `OutboxInterceptor` maps its domain events to integration events, using the
  module's `IIntegrationEventMapper` (keyed by DbContext type), and stores them in the module's own
  `OutboxMessages` table in the same transaction.
- **A background processor per publishing module delivers them:**
  - it claims messages with `UPDATE … WHERE Id IN (SELECT … FOR UPDATE SKIP LOCKED) RETURNING *` and a lease;
  - it delivers to each subscriber in its own scope;
  - it tracks `DeliveredTo` per subscriber, retries with exponential backoff, and dead-letters after
    `MaxAttempts`.
- **Subscribers are idempotent.** They dispatch an `IIdempotentCommand` keyed by the event id; the
  module inbox and unit of work make the effect happen once.
- **Settings are configurable:** `Outbox:PollingInterval`, `BatchSize`, `MaxAttempts`, `RetryBaseDelay`
  and `LeaseDuration`.

## Consequences

- No event is lost or published for a rolled-back change. Delivery is at-least-once and eventually
  consistent.
- One failing subscriber never blocks or repeats the others.
- **Operating dead letters.** Every publishing module exposes its outbox through `IOutboxAdministration`,
  and the Operations module serves it to the back-office:
  - `GET /api/operations/outbox` shows pending and dead-lettered messages per module;
  - `GET /api/operations/outbox/dead-letters` shows what failed and the last error;
  - `POST /api/operations/outbox/{module}/dead-letters/{id}/replay` puts a message back in the queue once
    the cause is fixed. Subscribers in `DeliveredTo` are not called again.
- Moving to a broker later means one more subscriber (the "forwarder") that publishes outbox messages
  to it; the producers don't change.
