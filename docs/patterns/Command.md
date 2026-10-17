# Command

## Problem

Back-office staff run powerful operations: *"raise every price of this importer by 8% after the
metical slid against the rand"*, *"suspend this seller while we investigate a fraud report"*. They
must be:

- **audited:** who did what, and when;
- **reversible:** undo the price change exactly, or lift the suspension;
- **safe to undo:** don't overwrite prices someone changed afterwards, and don't undo twice.

## Naive solution

```csharp
[HttpPost("sellers/{id}/adjust-prices")]
public async Task Adjust(Guid id, decimal pct)
{
    foreach (var p in db.Products.Where(p => p.SellerId == id)) p.Price *= 1 + pct / 100;
    db.Audit.Add(new Audit($"{User.Identity!.Name} adjusted {id} by {pct}%"));  // a string: can't be undone
    await db.SaveChangesAsync();
}
// Undo? The old prices are gone. A second endpoint "adjust by -pct" doesn't restore them (rounding).
```

## Solution

Each operation is an object implementing `IBackofficeOperation` with `ExecuteAsync` and `UndoAsync`.
While executing, it **captures the state it needs to undo**: `AdjustSellerPricesOperation` records
every `(product, old price, new price)`. `BackofficeInvoker` handles the rest:

- **On execute:** runs the operation, serializes it as polymorphic JSON (the `$kind` discriminator)
  and appends a `BackofficeOperationRecord` with the actor and time to the audit log.
- **On undo:** loads the record, deserializes the operation and calls `UndoAsync`, then marks the
  record as undone, again with the actor and time.

```mermaid
sequenceDiagram
    participant C as BackofficeController
    participant M as IMediator
    participant I as BackofficeInvoker
    participant O as AdjustSellerPricesOperation
    participant L as Audit log (BackofficeOperations)
    C->>M: AdjustSellerPrices(seller, +10%, reason)
    M->>I: Execute(operation)
    I->>O: ExecuteAsync(context)
    O->>O: change prices, remember old ones
    I->>L: Add(kind, description, JSON payload, actor, time)
    Note over C,L: later
    C->>M: UndoBackofficeOperation(id)
    M->>I: Undo(id)
    I->>L: Get(id) → payload
    I->>O: deserialize → UndoAsync(context)
    O->>O: refuse if prices changed since; else restore
    I->>L: MarkUndone(actor, time)
```

Safety rules:

- **Undo refuses on conflict.** If any product's current price differs from the price the operation
  set, undo answers `409 catalog.undo_conflict` and lists the SKUs.
- **One undo per operation.** A second undo is a `409`.
- **Failed operations leave no trace.** They aren't recorded.
- **Everything is atomic.** It all runs inside the Mediator's unit of work, so the operation, its
  audit record and its undo commit together.
- **Every endpoint needs the role.** All endpoints require a JWT with the `backoffice` role, and
  `ICurrentActor` supplies the name.

The GoF *Command* (an operation object you can execute, store and undo) is distinct from the
Mediator's commands (`ICommand`), which are request messages. Here the Mediator command
`AdjustSellerPrices` *creates and invokes* a back-office operation object.

## Where in the code

- Operations and invoker:
  [Backoffice/](../../src/Modules/Catalog/Kanimambo.Catalog.Application/Backoffice)
  (`BackofficeOperations.cs`, `BackofficeInvoker.cs`, `BackofficeRequests.cs`)
- Audit record:
  [BackofficeOperationRecord.cs](../../src/Modules/Catalog/Kanimambo.Catalog.Domain/Backoffice/BackofficeOperationRecord.cs)
  (`jsonb` payload)
- Endpoints:
  [BackofficeController.cs](../../src/Modules/Catalog/Kanimambo.Catalog.Api/Controllers/BackofficeController.cs)

## Tests

- [BackofficeOperationTests](../../tests/Kanimambo.Catalog.UnitTests/Command/BackofficeOperationTests.cs)
  cover:
  - the audit record (who, when, what);
  - exact restore on undo, by a different actor;
  - the conflict when a price changed afterwards;
  - undoing only once;
  - suspension and its undo;
  - no record when an operation fails;
  - the serialization round-trip.
- [BackofficeApiTests](../../tests/Kanimambo.Catalog.IntegrationTests/BackofficeApiTests.cs) cover:
  - 401/403 for missing or wrong roles;
  - execute, audit, undo and a second undo returning 409, against PostgreSQL;
  - a suspended seller vanishing from search and coming back after undo.

## Trade-offs & when NOT to use

- **Undo is domain-specific.** Each operation must decide what "undo" means and when it is unsafe.
  Some operations can't be undone at all; an SMS already sent can only be compensated.
- **Stored payloads must stay readable.** Payloads outlive code changes: renaming a property breaks
  undo for old records. Version the `$kind` or keep old shapes deserializable.
- **Overkill for plain CRUD.** For edits nobody needs to undo, an ordinary command handler plus an
  audit-trail interceptor is enough.
