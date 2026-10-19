# 0009 – Shipping: strategies for quotes, one consignment per order, couriers by webhook

- **Status:** accepted
- **Date:** 2026-10-18

## Context

A marketplace order can contain products from several sellers, in different cities. Each seller hands
over their own goods, and they travel separately:

- by motorbike or txopela within Maputo/Matola;
- by courier to the provinces;
- or to a pickup point.

The customer pays one delivery fee at checkout. The order should count as delivered (and a
cash-on-delivery order as paid) only when every part has arrived. Couriers report progress
asynchronously, retry their webhooks, and sometimes send them out of order. Real courier sandboxes
need contracts and credentials.

## Decision

- **Quotes: Strategy, synchronous.**
  - Ordering's checkout calls `Kanimambo.Shipping.Contracts.IShippingQuoter`, with one parcel per
    seller, leaving from the seller's city (`SellerSnapshot.City` was added for this).
  - The customer may name a method (`SameDay`, `Courier`, `Pickup`), a district and a pickup point.
    Otherwise `Shipping:DefaultMethods` picks the first method that can serve the order.
  - Every price, zone, vehicle, rate, transit time, cut-off and pickup point is configuration.
- **The order keeps what it was sold with:**
  - the delivery method, district, pickup point and estimated date (`OrderDelivery`);
  - the parcels' sellers, origin cities and weights (`OrderParcels`).
  - On `Dispatch` it raises `OrderDispatched` with that data, plus the cash the courier must collect.
- **Shipping keeps one `Consignment` aggregate per order** (unique OrderId), with one `Parcel` per seller.
  - **Booking.** The subscriber (`ShipOrder`, idempotent) re-plans with the same strategy to know each
    parcel's courier and vehicle, then books every parcel through that courier's adapter.
  - **Parcel references** are deterministic (`KM-1002-1`). A retried booking is therefore recognised by
    the courier, which returns the existing waybill.
  - **Outages** throw, so nothing is committed and the outbox retries.
  - **Refusals** fail the parcel.
  - **Cash on delivery** is collected with the first parcel.
- **Tracking.**
  - Couriers post signed webhooks to `POST /api/shipping/webhooks/{courier}`; invalid signatures
    return 401.
  - Parcel status only moves forward (a rank, not a State pattern: the lifecycle is linear), so
    repeated or late webhooks are acknowledged with 204 and change nothing.
  - The consignment derives its own status from its parcels, and raises `ConsignmentDelivered` once,
    when the last parcel arrives.
  - Concurrent webhooks for sibling parcels conflict on the consignment's `xmin`. The loser gets a 409
    and the courier retries, so the "last parcel" moment can't be missed.
- **Pickup.** "Delivered" at a pickup point means *ready for collection*. Staff confirm the
  collection (`POST /api/shipping/consignments/{id}/collected`), and only then is the order delivered.
- **Outcomes.**
  - `OrderDelivered` makes Ordering apply `Deliver`, which is when cash orders are paid.
  - `ParcelStatusChanged` drives the customer's delivery messages (Notifications, ADR 0012), and
    `DeliveryFailed` is published for back-office tooling. A failed delivery leaves the order
    `OutForDelivery`, for the back-office to resolve.
- **`Kanimambo.Simulators.Courier`** plays two **fictional** couriers in deliberately different
  simulated formats:
  - **Txova Express**, for same-day riders;
  - **Mbila Cargo**, for inter-provincial cargo.
  - Tracking events follow configurable delays, and webhooks are signed and retried until
    acknowledged.
  - The last four digits of the recipient's phone pick the scenario: `0101` recipient absent, `0102`
    booking refused, `0103` outage on the first booking request, anything else succeeds.

## Consequences

- **Asynchronous delivery.** Checkout answers with a fee and an estimated date. Delivery happens later
  through events and needs no back-office step, except for pickup collection.
- **Two quotes.** Checkout and dispatch both quote. The customer pays the checkout fee, while the
  courier choice follows the configuration at dispatch time.
- **Splitting cash between parcels** (several couriers collecting parts of the cash) is not supported.
  The first parcel's courier collects the whole amount.
- **Connecting a real courier** means one new adapter class and its connection settings. The secrets
  in `appsettings.json` are development placeholders that match the simulator.
- **Interim code removed.** `InterimShippingQuoter` and `Ordering:InterimShipping` are gone.
