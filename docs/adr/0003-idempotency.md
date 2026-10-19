# 0003 – Idempotency: HTTP `Idempotency-Key` plus a command inbox

- **Status:** accepted
- **Date:** 2026-10-17

## Context

Customers and couriers often lose connectivity mid-request and retry. A duplicated checkout or
payment initiation charges the customer twice, through M-Pesa, e-Mola or mKesh. Duplicates also come
from inside the system: the outbox relay may deliver an event twice, and mobile-money gateways may
re-send callbacks.

## Decision

The two sources of duplicates are handled at different layers.

### HTTP: `IdempotencyMiddleware` (host)

- Every `POST`/`PUT`/`PATCH`/`DELETE` accepts the `Idempotency-Key` header. Endpoints marked with
  `[RequiresIdempotencyKey]` (checkout, payment initiation) reject requests without it (400).
- Records are keyed by **(method + path, key)** and store a SHA-256 fingerprint of the query string
  and body.
- The first request inserts an *in-progress* row with `INSERT … ON CONFLICT DO NOTHING`. The primary
  key acts as the lock. Its response is then stored.
- What other requests with the same key get:

  | Situation | Response |
  |---|---|
  | Same payload, first request completed | The stored response, replayed with `Idempotent-Replayed: true` |
  | Different payload | 422 (key reuse is a client bug) |
  | First request still in flight | 409 (retry later) |

- 5xx responses and exceptions are **not** stored: the row is released so the client can retry.
- Records expire after 24 h (configurable). An in-progress row older than 2 minutes is treated as
  abandoned. A background service deletes expired rows.
- Storage is **PostgreSQL** (`platform.IdempotencyRecords`), not Redis. A lost Redis key would turn
  into a double charge, so durability matters more than latency here.

### In-process: `IdempotencyBehavior` (Mediator)

- Commands implementing `IIdempotentCommand` carry their own key. A duplicate returns
  `Result.Success()` without running the handler.
- The processed key is written through the module's `IInboxStore` inside the same unit of work, so it
  is committed only together with the command's effects (see ADR 0002).

## Consequences

- Clients must generate a fresh key (a UUID) per logical operation and reuse it across retries.
- Fingerprints are byte-level: semantically equal JSON with different formatting counts as a
  different payload.
- Responses are buffered in memory to be stored, which is fine for JSON write endpoints. Streaming
  endpoints must not be write endpoints.
- The caller's identity is part of the record scope (method, path and signed-in user), so two
  customers who send the same key never receive each other's stored response. Anonymous callers
  (guest carts) share one scope; their keys are random GUIDs generated on the device.
