# Adapter

## Problem

The three mobile-money operators each speak a different API: different field names, error codes,
amount formats, ways of querying status, and callback formats. The rest of the system wants
one question ("charge this phone this amount, call me back here") and one vocabulary of outcomes
(`InsufficientBalance`, `DuplicateTransaction`, `InvalidNumber`, `Timeout`…). Support staff
still need each operator's native code when a customer calls.

> The formats below are the ones implemented by **our local simulator**
> (`Kanimambo.Simulators.MobileMoney`). They are deliberately different from each other to show what
> an adapter absorbs. They are **not** the official APIs of Vodacom, Movitel or Tmcel; a real adapter
> needs each operator's sandbox credentials and documentation.

| | M-Pesa (simulated) | e-Mola (simulated) | mKesh (simulated) |
|---|---|---|---|
| Request | `POST mpesa/c2b`, `input_*` fields | `POST emola/payments`, camelCase JSON | `POST mkesh/v1/cobrancas`, Portuguese fields |
| Amount | `"1234.50"` (string) | `123450` (cents) | `"1234,50"` (decimal comma) |
| Result | `output_ResponseCode`: `INS-0`, `INS-5`, `INS-6`, `INS-10` | `status`: `PENDING` / `REJECTED` + `errorCode` | numeric `codigo`: 0, 94, 51, 14 |
| Status | `output_ResponseTransactionStatus` | `SUCCESS` / `FAILED` / `PENDING` | `CONCLUIDA` / `FALHADA` / `PENDENTE` |

Callbacks from all three carry `X-Signature: sha256=<HMAC of the body>`.

## Naive solution

```csharp
if (op == "MPesa") { var r = await mpesa.C2B(...); if (r.output_ResponseCode == "INS-6") ... }
else if (op == "EMola") { var r = await emola.Pay(...); if (r.errorCode == "BALANCE_LOW") ... }
// Operator codes leak into the domain, error handling is copied per operator,
// and the callback endpoint parses three formats inline.
```

## Solution

`IMobileMoneyGateway` is the **target** interface, defined in Application, in the system's own
terms:

- `RequestPaymentAsync(MobileMoneyRequest)` returns `GatewayAcceptance` or a `GatewayError`;
- `QueryStatusAsync(paymentId)` returns a `GatewayPaymentStatus`;
- `ParseCallback(body, signature)` verifies the HMAC and translates the payload.

Each operator has an **adapter** in Infrastructure: `MPesaAdapter`, `EMolaAdapter` and
`MKeshAdapter`. Each one:

- serialises the request in its operator's format;
- maps native codes to `PaymentFailureReason`, keeping the native code in `GatewayError.NativeCode`
  and on the payment;
- turns 5xx responses, timeouts and connection failures into `GatewayUnavailable` / `Timeout`, both
  marked `IsTransient`, so the retry decorator knows what is safe to repeat.

The shared HTTP and signature plumbing is in `GatewayHttp` and `WebhookSignature` (shared with the courier adapters). The translation
of each format stays inside its own adapter.

```mermaid
classDiagram
    class IMobileMoneyGateway {
        <<interface>>
        +Operator
        +RequestPaymentAsync(request) Result~GatewayAcceptance~
        +QueryStatusAsync(paymentId) Result~GatewayPaymentStatus~
        +ParseCallback(body, signature) Result~GatewayPaymentStatus~
    }
    class MPesaAdapter { input_* · INS-* }
    class EMolaAdapter { cents · BALANCE_LOW }
    class MKeshAdapter { valor "1,23" · codigo 51 }
    IMobileMoneyGateway <|.. MPesaAdapter
    IMobileMoneyGateway <|.. EMolaAdapter
    IMobileMoneyGateway <|.. MKeshAdapter
    MPesaAdapter --> HttpClient : mobile-money-MPesa
    EMolaAdapter --> HttpClient : mobile-money-EMola
    MKeshAdapter --> HttpClient : mobile-money-MKesh
    MobileMoneyProcessor --> IMobileMoneyGateway
    PaymentHandlers --> IMobileMoneyGateway : callbacks, status
```

The callback endpoint (`POST /api/payments/callbacks/{operator}`) is anonymous and generic. It passes
the raw body and signature to the operator's adapter, so a forged or tampered callback is rejected
with `401 payments.invalid_signature` before anything else happens.

### Couriers (Shipping)

The same problem appears with couriers. `ICourierGateway` (`BookAsync`, `ParseWebhook`) is the target,
with one adapter per courier.

> Both couriers are **fictional**, and their APIs are the ones implemented by the local simulator
> (`Kanimambo.Simulators.Courier`). A real courier integration needs that courier's documentation and
> sandbox credentials.

| | Txova Express (same-day riders, simulated) | Mbila Cargo (inter-provincial, simulated) |
|---|---|---|
| Booking | `POST txova/v1/deliveries`, English JSON with nested `pickup` / `dropoff` | `POST mbila/api/guias`, Portuguese fields (`origem`, `destino`, `morada`) |
| Weight / cash | `weightKg: 12.25`, `cashToCollect: 1234.5` | `pesoGramas: 12250`, `valorCobranca: "1234,50"` |
| Vehicle | `MOTO` / `TXOPELA` (from our `Motorbike` / `Txopela`) | — |
| Refusal | HTTP 422 `{error: OUT_OF_AREA \| TOO_HEAVY}` | HTTP 200 `{codigo: E12 \| E30 \| E41}` |
| Tracking webhook | `event`: `PICKED_UP`, `DELIVERED`, `FAILED` + `reason` | `estado`: `RECOLHIDO`, `EM_TRANSITO`, `ENTREGUE`, `DEVOLVIDO` + `observacao` |

**What the courier adapters do:**

- They map refusals to `CourierFailureReason` (`DestinationNotServed`, `Overweight`,
  `InvalidRecipient`, `Rejected`), keeping the native code.
- They turn 5xx responses, timeouts and connection failures into a transient `Unavailable`. The
  booking is then retried through the outbox.
- They translate each courier's status words into our `ParcelStatus`.

**Why bookings can be retried safely:** our parcel reference (`KM-1002-1`) is deterministic, and the
courier returns the existing waybill for a reference it already knows. For the same reason, couriers
keep ServiceDefaults' standard HTTP resilience, unlike the payment operators.

**Shared plumbing:** webhook signatures use the same `WebhookSignature` helper as the payment
callbacks (`Kanimambo.SharedKernel.Infrastructure.Webhooks`).

## Where in the code

- Target: [IMobileMoneyGateway.cs](../../src/Modules/Payments/Kanimambo.Payments.Application/Gateways/IMobileMoneyGateway.cs), [GatewayError.cs](../../src/Modules/Payments/Kanimambo.Payments.Application/Gateways/GatewayError.cs)
- Adapters: [MPesaAdapter.cs](../../src/Modules/Payments/Kanimambo.Payments.Infrastructure/Gateways/Adapters/MPesaAdapter.cs), [EMolaAdapter.cs](../../src/Modules/Payments/Kanimambo.Payments.Infrastructure/Gateways/Adapters/EMolaAdapter.cs), [MKeshAdapter.cs](../../src/Modules/Payments/Kanimambo.Payments.Infrastructure/Gateways/Adapters/MKeshAdapter.cs),
  [GatewayHttp.cs](../../src/Modules/Payments/Kanimambo.Payments.Infrastructure/Gateways/Adapters/GatewayHttp.cs)
- Simulator: [MPesaController.cs](../../src/Simulators/Kanimambo.Simulators.MobileMoney/MPesaController.cs), [EMolaController.cs](../../src/Simulators/Kanimambo.Simulators.MobileMoney/EMolaController.cs), [MKeshController.cs](../../src/Simulators/Kanimambo.Simulators.MobileMoney/MKeshController.cs)
- Callback endpoint: [PaymentsController.cs](../../src/Modules/Payments/Kanimambo.Payments.Api/Controllers/PaymentsController.cs)
- Couriers:
  - target: [ICourierGateway.cs](../../src/Modules/Shipping/Kanimambo.Shipping.Application/Couriers/ICourierGateway.cs), [CourierError.cs](../../src/Modules/Shipping/Kanimambo.Shipping.Application/Couriers/CourierError.cs)
  - adapters: [TxovaAdapter.cs](../../src/Modules/Shipping/Kanimambo.Shipping.Infrastructure/Couriers/TxovaAdapter.cs), [MbilaAdapter.cs](../../src/Modules/Shipping/Kanimambo.Shipping.Infrastructure/Couriers/MbilaAdapter.cs)
  - simulator: [TxovaController.cs](../../src/Simulators/Kanimambo.Simulators.Courier/TxovaController.cs), [MbilaController.cs](../../src/Simulators/Kanimambo.Simulators.Courier/MbilaController.cs)
  - webhook endpoint: `POST /api/shipping/webhooks/{courier}` in
    [ShippingController.cs](../../src/Modules/Shipping/Kanimambo.Shipping.Api/Controllers/ShippingController.cs)

## Tests

- [OperatorAdapterTests](../../tests/Kanimambo.Payments.UnitTests/Adapter/OperatorAdapterTests.cs),
  against a stub HTTP handler:
  - ten native refusal codes map to the shared reasons with the native code kept;
  - accepted requests return the operator's transaction id;
  - each operator receives the amount in its own format;
  - 5xx and connection errors are transient;
  - status queries and signed callbacks are translated;
  - missing, forged and tampered signatures are rejected.
- [MobileMoneyPaymentTests](../../tests/Kanimambo.Payments.IntegrationTests/MobileMoneyPaymentTests.cs):
  the three operators end to end through the simulator, including native codes on failed payments
  and forged callbacks returning 401.
- [CourierAdapterTests](../../tests/Kanimambo.Shipping.UnitTests/Adapter/CourierAdapterTests.cs):
  - Txova gets kilograms and its vehicle codes;
  - Mbila gets grams and a decimal-comma amount;
  - six native refusals map to the shared reasons;
  - outages are transient;
  - seven webhook states are translated;
  - bad signatures and unknown states are rejected, never guessed.
- [DeliveryTests](../../tests/Kanimambo.Shipping.IntegrationTests/DeliveryTests.cs):
  - both couriers end to end through the simulator;
  - a refused booking keeps Mbila's reason;
  - a booking outage is retried under the same reference;
  - forged webhooks return 401.

## Trade-offs & when NOT to use

- **Shared vocabulary loses detail.** The native code is kept for support, but logic should never
  branch on it. If it starts to, the shared enum is missing a case.
- **One adapter per operator is the right size here.** A generic, configuration-driven "JSON mapping"
  adapter looks attractive but turns code into untestable configuration.
- **If you control both sides, you don't need it.** Make them agree on one contract instead.
