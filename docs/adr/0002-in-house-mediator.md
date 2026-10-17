# 0002 – In-house Mediator instead of MediatR

- **Status:** accepted
- **Date:** 2026-10-17

## Context

Controllers should depend only on a dispatcher, and cross-cutting concerns should be applied once, as
pipeline behaviors. MediatR is the usual choice but now requires a commercial license. Implementing
the Mediator pattern is also part of what this repository showcases.

## Decision

- Implement `IMediator` (`ISender` + `IPublisher`) in `Kanimambo.SharedKernel.Messaging`.
- Make commands and queries explicit (`ICommand`, `ICommand<T>`, `IQuery<T>`), always returning
  `Result`/`Result<T>`.
- **Behaviors run in registration order**, with the first one registered as the outermost. The order
  is declared in one place, `Program.cs`:
  1. `LoggingBehavior` – structured log and OpenTelemetry span for every request;
  2. `ValidationBehavior` – runs `IValidator<T>` and short-circuits with a `ValidationError`;
  3. `UnitOfWorkBehavior` – commands only (generic constraint): commits the owning module's transaction
     on success and rolls back on failure;
  4. `IdempotencyBehavior` – `IIdempotentCommand` only: inbox de-duplication.
- `IdempotencyBehavior` runs **inside** the unit of work. This changes the order first proposed in the
  Phase 0 plan, which placed it before the unit of work. Inside, the "processed" marker is committed
  atomically with the command's changes. A failed command doesn't mark its key, so a later
  re-delivery runs again. Concurrent duplicates are resolved by the inbox's unique key at commit time.
- Applicability is expressed through **generic constraints**. Microsoft DI skips open-generic
  implementations whose constraints are not satisfied when it resolves `IEnumerable<>`.
- Validation uses a small `IValidator<T>` interface instead of FluentValidation, to keep dependencies
  minimal. Rules that need state (stock, uniqueness) belong in the domain.

## Consequences

- No licensing risk, and the dispatch path is small enough to read in one sitting.
- Features MediatR offers (stream requests, pre/post processors) are not available unless implemented
  here. None are needed today.
- Handler discovery is by assembly scanning at startup. A request with two handlers fails fast at
  registration.
