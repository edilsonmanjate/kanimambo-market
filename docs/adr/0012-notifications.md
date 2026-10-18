# 0012 – Notifications: channels chosen per customer, fallback instead of HTTP retries

- **Status:** accepted
- **Date:** 2026-10-18

## Context

Customers must be told about payments, payment failures, deliveries and invoices.

- **SMS reaches every phone** but is limited to 160 GSM-7 characters.
- **WhatsApp and e-mail** reach fewer people but carry more: links, details, documents.
- **Providers fail in two ways.** Either the number isn't on WhatsApp, or the SMS gateway is down.
  Re-posting a message that was in fact accepted sends it twice, and customers notice duplicate SMS.

## Decision

- **Subscribers.** The Notifications module subscribes, through the other modules' contracts, to:
  - `OrderPaid`, which produces a confirmation;
  - `OrderStatusChanged` → PaymentFailed, which produces a reminder with the reason;
  - `ParcelStatusChanged` for PickedUp, ReadyForCollection, Delivered and Failed, which produces
    delivery updates;
  - `FiscalDocumentIssued`, which produces an "invoice available" message.
  Each one becomes an idempotent `Notify` command keyed by the event id.
- **Channel choice.** `ContactPreferences`, keyed by phone, owned by the account that registered it,
  and editable only by that account or the back-office, order the channels. The default is SMS. A
  message may restrict its channels; invoices go by e-mail or WhatsApp only.
- **Fallback, not HTTP retries.** The first channel that delivers wins, and every attempt is recorded
  in `SentNotifications` for support.
  - The channel HttpClients opt out of the generic resilience handler. A failing channel falls back at
    once instead of retrying (and possibly double-sending) for seconds.
  - When every attempted channel failed temporarily, the command fails and the outbox retries later.
    Permanent refusals are recorded and not retried.
- **Text rules live in the channel.** The SMS channel converts to GSM-7 and fits 160 characters.
  `Kanimambo.Simulators.Messaging` enforces the same rules (it rejects non-GSM-7 text and
  161 characters), so a regression fails the tests.
- **Simulator scenarios.** Phones ending in `0201` meet an SMS gateway outage, and phones ending in
  `0202` are not on WhatsApp. The simulator's `GET /inbox?to=` shows what each phone or mailbox
  received.

## Consequences

- **At-least-once delivery.** A message accepted by a provider whose acknowledgement was lost can still
  be sent twice when the outbox retries. Providers' idempotency keys would close that gap with real
  integrations.
- **Ownership isn't verified.** Phone ownership for preferences is first-come; a real deployment needs
  an OTP confirmation by SMS first.
- **Sellers aren't notified yet.** Their contact data isn't modelled; it would be another refined
  abstraction (a message type) using the same channels.
- **The gateway formats are the simulator's.** Real SMS aggregators, WhatsApp Business and e-mail
  providers each need an adapter and credentials.
