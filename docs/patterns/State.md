# State

## Problem

An order moves through `Pending → AwaitingPayment → Paid → Preparing → OutForDelivery → Delivered`, and
can branch to `Cancelled`, `PaymentFailed` or `Refunded`. What is allowed depends on the status, and in
Mozambique also on how the customer pays:

- an M-Pesa order must be paid before it is prepared;
- a cash-on-delivery order is prepared straight away, and only counts as paid when the courier collects
  the money;
- a paid order is refunded, not cancelled.

An invalid transition (dispatching an unpaid M-Pesa order, refunding an unpaid cash order) must be
impossible, not merely discouraged.

## Naive solution

```csharp
public void Dispatch()
{
    if (Status == "Preparing") Status = "OutForDelivery";
    else if (Status == "Paid" && PaymentMethod == "COD") ...   // which combinations are valid? scattered
    else throw new InvalidOperationException();
}
// …the same switch repeated in Cancel, Refund, Deliver; one forgotten case ships an unpaid order.
```

## Solution

Each status is an `OrderState` object. Every action is **refused by default** in the base class, and a
state opens only the transitions it overrides:

- `Order` delegates each action to its current state (`Order.Dispatch(now) => State.Dispatch(this, now)`)
  and only knows how to **record** a transition. That means status history, an `OrderStatusChanged` event,
  and `OrderPaid` when payment happens.
- The payment rule lives in the states. `Pending` accepts `RequestPayment` for prepaid orders but
  `StartPreparing` only for cash on delivery; `OutForDelivery.Deliver` raises `OrderPaid` for cash orders.
- `AllowedActions` is derived from the same states, so the API shows clients exactly what they can do next.

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> AwaitingPayment: RequestPayment (prepaid)
    Pending --> Preparing: StartPreparing (cash on delivery)
    Pending --> Cancelled: Cancel
    AwaitingPayment --> Paid: ConfirmPayment
    AwaitingPayment --> PaymentFailed: FailPayment
    AwaitingPayment --> Cancelled: Cancel
    PaymentFailed --> AwaitingPayment: RetryPayment
    PaymentFailed --> Cancelled: Cancel
    PaymentFailed --> Paid: ConfirmPayment (late confirmation)
    Paid --> Preparing: StartPreparing
    Paid --> Refunded: Refund
    Preparing --> OutForDelivery: Dispatch
    Preparing --> Refunded: Refund (prepaid)
    Preparing --> Cancelled: Cancel (cash on delivery)
    OutForDelivery --> Delivered: Deliver (cash orders are paid here)
    Delivered --> Refunded: Refund
    Cancelled --> [*]
    Refunded --> [*]
```

`PaymentFailed → Paid` handles a **late confirmation**. The customer typed the PIN after the payment
timed out, and the operator did take the money, so the order counts as paid. Failures from an older
attempt are ignored once the customer has retried.

### Payment lifecycle (Payments)

A mobile-money payment is asynchronous:

1. the operator accepts the request and pushes a PIN prompt;
2. the customer confirms on the phone, or not;
3. the result arrives later by callback, or never arrives.

`PaymentState` applies the same technique to this lifecycle:

```mermaid
stateDiagram-v2
    [*] --> Initiated
    Initiated --> AwaitingCustomerPin: operator accepted (PIN prompt sent)
    Initiated --> Failed: refused upfront (unknown prefix, duplicate, invalid number…)
    AwaitingCustomerPin --> Confirmed: callback / status = confirmed
    AwaitingCustomerPin --> Failed: callback / status = failed (e.g. insufficient balance)
    AwaitingCustomerPin --> TimedOut: PIN deadline passed and the operator still says pending
    TimedOut --> Confirmed: late callback (the money was taken)
    Confirmed --> [*]
    Failed --> [*]
```

- **Timeouts.** `PaymentTimeoutMonitor` sends `ExpirePendingPayments` every
  `Payments:TimeoutCheckInterval`. Before timing a payment out, it asks the operator for the status
  once more, because the callback may simply have been lost.
- **Duplicates.** Duplicate and out-of-order callbacks are refused by the state, acknowledged with 204
  (so the operator stops retrying), and logged.
- **Retries.** Every retry is a new `Payment` with the next attempt number, unique per
  (order, attempt). The history of each attempt stays intact.

## Where in the code

- [OrderStates.cs](../../src/Modules/Ordering/Kanimambo.Ordering.Domain/OrderStates.cs) (states)
- [Order.cs](../../src/Modules/Ordering/Kanimambo.Ordering.Domain/Order.cs) (context)
- [OrderRequests.cs](../../src/Modules/Ordering/Kanimambo.Ordering.Application/OrderRequests.cs)
  (`ChangeOrderStatus`)
- `POST /api/orders/{id}/transitions/{transition}` (back-office), and `POST /api/orders/{id}/cancel`
  and `POST /api/orders/{id}/retry-payment` (owner)
- Payments:
  - [PaymentStates.cs](../../src/Modules/Payments/Kanimambo.Payments.Domain/PaymentStates.cs) (states)
  - [Payment.cs](../../src/Modules/Payments/Kanimambo.Payments.Domain/Payment.cs) (context)
  - `PaymentTimeoutMonitor` in
    [PaymentsInfrastructure.cs](../../src/Modules/Payments/Kanimambo.Payments.Infrastructure/PaymentsInfrastructure.cs)
- Payment outcomes reach Ordering through the outbox:
  [PaymentOutcomes.cs](../../src/Modules/Ordering/Kanimambo.Ordering.Application/Payments/PaymentOutcomes.cs)

## Tests

- [OrderStateTests](../../tests/Kanimambo.Ordering.UnitTests/State/OrderStateTests.cs) are
  **exhaustive**: every reachable state × every action, for prepaid and cash-on-delivery orders
  (270 cases).
  - An allowed action reaches the expected state and records history.
  - Anything else is refused, leaves the status unchanged and raises no event.
  - `AllowedActions` matches the table.
  - `OrderPaid` fires on confirmation (prepaid) or on delivery (cash).
- [LifecycleAndOutboxTests](../../tests/Kanimambo.Ordering.IntegrationTests/LifecycleAndOutboxTests.cs)
  cover the full cash-on-delivery flow over HTTP, and an invalid transition returning
  `409 ordering.invalid_transition`.
- [PaymentStateTests](../../tests/Kanimambo.Payments.UnitTests/State/PaymentStateTests.cs) are exhaustive
  as well: 5 statuses × 4 actions, plus the events raised on confirmation, failure and timeout.
- [MobileMoneyPaymentTests](../../tests/Kanimambo.Payments.IntegrationTests/MobileMoneyPaymentTests.cs)
  drive the payment states through the simulator:
  - confirmed;
  - insufficient balance;
  - duplicate;
  - no answer leading to `TimedOut`;
  - a delayed callback, where the order goes `AwaitingPayment → PaymentFailed → Paid` and the payment
    goes `AwaitingCustomerPin → TimedOut → Confirmed`;
  - a retried payment.

## Trade-offs & when NOT to use

- **One class per state.** For three states and two transitions, an enum and a guard clause read better.
- **Payment-dependent rules sit inside the states.** If more such variations appear, a state
  per (status, payment kind) or a transition table may scale better.
- **Persistence still uses an enum.** The status is stored as text; the state object is looked up from
  it. Never persist state objects.
