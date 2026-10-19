# Demo: one order through the whole system

This is the transcript of a real run of the demo on 18 October 2026, against a local AppHost
(`dotnet run --project src/Kanimambo.AppHost --launch-profile http`):

```bash
dotnet run scripts/demo.cs
```

[`scripts/demo.cs`](../scripts/demo.cs) runs [`http/kanimambo-journey.http`](../http/kanimambo-journey.http) from top
to bottom. Where the work is asynchronous (the operator's payment callback, the couriers' tracking), it
waits and polls. Each step prints the request, the status code (and how long it waited) and the
beginning of the response.

**What to look for:**

| Step | What it shows |
|---|---|
| 8 | The order is `AwaitingPayment`. The seeded promotion took 5 % off for paying by M-Pesa in Maputo, and both sellers' parcels need a txopela (25 kg+), so delivery costs 2 × 350 MZN. |
| 9 | About 6 seconds later the order is `Paid`. Nobody touched it: the simulated customer typed the PIN, and the operator's signed callback went through Payments and the outbox to Ordering. |
| 11 | Three invoices: `FS…` from the ISPC seller (no IVA), `FT…` from the IVA-normal importer, and the marketplace's delivery invoice. |
| 13 | The SMS, in GSM-7 (no accents) and within 160 characters. |
| 16–17 | After dispatch the couriers deliver both parcels, and the order becomes `Delivered`. |
| 18 | The importer's monthly e-Declaração CSV and IVA summary. |

Values such as ids, numbers and dates change on every run. Products and demo sellers come from the
development seed.

```text
1. Specification: groceries in stock delivered to Maputo, filters translated to SQL
   GET http://localhost:5080/api/catalog/products?q=Leite&inStock=true&deliversTo=Maputo  →  200
     {
       "items": [
         {
           "id": "01a14b97-3a68-79fc-950f-3db8d982f3b8",
           "sku": "IL-LEITE-12",
           "name": "Leite UHT",
           "category": "laticínios",
           "kind": "Simple",
           "price": 215.00,
           "currency": "ZAR",
           "priceMzn": 760.58,
           "availableStock": 40,
           "weightKg": 12.400,
           "sellerId": "01920000-0000-7000-8000-000000000005",
     …

2. Composite: the "Cabaz do Mês" bundle — price, stock derived from its components, IVA per tax category
   GET http://localhost:5080/api/catalog/products/01920000-0000-7000-9000-0000000000c1  →  200
     {
       "id": "01920000-0000-7000-9000-0000000000c1",
       "sellerId": "01920000-0000-7000-8000-000000000001",
       "sku": "MX-CABAZ-MES",
       "name": "Cabaz do Mês",
       "category": "cabazes",
       "kind": "Bundle",
       "variant": null,
       "price": {
         "amount": 3665.00,
         "currency": "MZN"
       },
       "priceMzn": {
         "amount": 3665.00,
     …

3. Memento: a guest cart (keep its id on the phone); every change is a snapshot
   POST http://localhost:5080/api/carts  →  201
     {
       "id": "17e3f831-327f-4aea-b420-a53c136b376d",
       "version": 1,
       "deliveryCity": "Maputo",
       "customerType": "guest",
       "lines": [],
       "itemCount": 0,
       "updatedAt": "2026-10-18T12:05:55.0497638+00:00",
       "checkedOutOrderId": null
     }

4. Add the bundle (sold by an ISPC seller)…
   PUT http://localhost:5080/api/carts/17e3f831-327f-4aea-b420-a53c136b376d/lines/01920000-0000-7000-9000-0000000000c1  →  200
     {
       "id": "17e3f831-327f-4aea-b420-a53c136b376d",
       "version": 2,
       "deliveryCity": "Maputo",
       "customerType": "guest",
       "lines": [
         {
           "productId": "01920000-0000-7000-9000-0000000000c1",
           "sku": "MX-CABAZ-MES",
           "name": "Cabaz do Mês",
           "quantity": 1
         }
       ],
       "itemCount": 1,
     …

5. …and the milk found in step 1 (priced in ZAR by an IVA-normal importer: converted to MZN — Proxy)
   PUT http://localhost:5080/api/carts/17e3f831-327f-4aea-b420-a53c136b376d/lines/01a14b97-3a68-79fc-950f-3db8d982f3b8  →  200
     {
       "id": "17e3f831-327f-4aea-b420-a53c136b376d",
       "version": 3,
       "deliveryCity": "Maputo",
       "customerType": "guest",
       "lines": [
         {
           "productId": "01920000-0000-7000-9000-0000000000c1",
           "sku": "MX-CABAZ-MES",
           "name": "Cabaz do Mês",
           "quantity": 1
         },
         {
           "productId": "01a14b97-3a68-79fc-950f-3db8d982f3b8",
     …

6. Null Object + Interpreter: the price in MZN with the best promotion for paying by M-Pesa
   GET http://localhost:5080/api/carts/17e3f831-327f-4aea-b420-a53c136b376d/pricing?paymentMethod=MPesa  →  200
     {
       "cartId": "17e3f831-327f-4aea-b420-a53c136b376d",
       "version": 3,
       "customerType": "guest",
       "deliveryCity": "Maputo",
       "paymentMethod": "MPesa",
       "lines": [
         {
           "productId": "01920000-0000-7000-9000-0000000000c1",
           "sku": "MX-CABAZ-MES",
           "name": "Cabaz do Mês",
           "quantity": 1,
           "unitPrice": 3665.00,
           "currency": "MZN",
     …

7. Strategy: every way to deliver this cart, cheapest first (the district picks the same-day zone)
   GET http://localhost:5080/api/shipping/options?cartId=17e3f831-327f-4aea-b420-a53c136b376d&district=KaMpfumo  →  200
     [
       {
         "method": "SameDay",
         "description": "Same-day delivery in Maputo / Matola by motorbike or txopela",
         "fee": 700,
         "estimatedDelivery": "2026-10-19",
         "pickupPointId": null,
         "parcels": [
           {
             "sellerId": "01920000-0000-7000-8000-000000000001",
             "fee": 350,
             "courier": "Txova",
             "vehicle": "Txopela",
             "estimatedDelivery": "2026-10-19"
     …

8. Facade + Chain of Responsibility: one checkout call (Idempotency-Key required: retrying returns the same order)
   POST http://localhost:5080/api/checkout  →  200
     {
       "orderId": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
       "number": 1007,
       "displayNumber": "KM-1007",
       "status": "AwaitingPayment",
       "subtotal": 5186.17,
       "promotionName": "M-Pesa em Maputo: 5% acima de 2 500 MZN",
       "discount": 259.31,
       "shippingFee": 700,
       "total": 5626.86,
       "deliveryMethod": "SameDay",
       "estimatedDelivery": "2026-10-19",
       "nextStep": "Confirm the MPesa payment of 5626.86 MZN on 258841237777.",
       "alreadyPlaced": false
     …

9. State: wait ~3 s and ask again — the simulated customer typed the PIN, the operator called back (Factory Method,
   Adapter, Decorator), Payments told Ordering through the outbox (Observer): AwaitingPayment → Paid
   GET http://localhost:5080/api/orders/c02a134f-18d6-454c-9ee8-f0d3ca7fd20f  →  200  (after 6 s)
     {
       "id": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
       "number": 1007,
       "displayNumber": "KM-1007",
       "status": "Paid",
       "allowedActions": [
         "StartPreparing",
         "Refund"
       ],
       "customerType": "guest",
       "deliveryCity": "Maputo",
       "paymentMethod": "MPesa",
       "paymentReference": "MP66949811",
       "lines": [
     …

10. The payment as the operator saw it: lifecycle and native codes (back-office)
   GET http://localhost:5080/api/payments?orderId=c02a134f-18d6-454c-9ee8-f0d3ca7fd20f  →  200
     [
       {
         "id": "03a722a5-6fb1-4656-9660-9b208a09eeb0",
         "orderId": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
         "orderNumber": 1007,
         "attempt": 1,
         "method": "MPesa",
         "operator": "MPesa",
         "phone": "258841237777",
         "amount": 5626.86,
         "status": "Confirmed",
         "operatorTransactionId": "MP66949811",
         "failureReason": null,
         "nativeCode": null,
     …

11. Builder + Abstract Factory: one invoice per seller in its own regime (FS… under ISPC, FT… with IVA) plus the
   delivery invoice; together they add up to the order total
   GET http://localhost:5080/api/invoicing/documents?orderId=c02a134f-18d6-454c-9ee8-f0d3ca7fd20f  →  200  (after 1 s)
     [
       {
         "id": "4fe0f80d-2bd2-43d8-bcf9-3126a59e8620",
         "type": "Invoice",
         "title": "Fatura",
         "number": "FS 2026/000004",
         "regime": "Ispc",
         "issueDate": "2026-10-18",
         "issuerNuit": "400100001",
         "issuerName": "Mercearia Xipamanine, Lda",
         "buyerNuit": null,
         "buyerName": "Consumidor final",
         "orderId": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
         "orderNumber": 1007,
     …

12. Visitor + Object Pool: the first document as PDF (feature flag Invoicing.InvoicePdf)
   GET http://localhost:5080/api/invoicing/documents/4fe0f80d-2bd2-43d8-bcf9-3126a59e8620/pdf  →  200
     [application/pdf, 2,427 bytes, starts with %PDF-1.4]

13. Bridge: what the customer was told, and on which channel (SMS by default: GSM-7, one message)
   GET http://localhost:5080/api/notifications/sent?recipient=258841237777  →  200  (after 0 s)
     [
       {
         "id": "9b0ba8f5-73a2-46f3-8bc5-dc40f4662cfc",
         "messageType": "OrderConfirmation",
         "channel": "Sms",
         "recipient": "258841237777",
         "orderNumber": 1007,
         "status": "Sent",
         "text": "Kanimambo! Recebemos o pagamento da encomenda KM-1007 (5 626,86 MZN). kanimambo.co.mz/e/KM-1007",
         "providerMessageId": "SM-d444f0b5cfa",
         "error": null,
         "at": "2026-10-18T12:06:04.729568+00:00"
       }
     ]

14. The sellers prepare the order…
   POST http://localhost:5080/api/orders/c02a134f-18d6-454c-9ee8-f0d3ca7fd20f/transitions/StartPreparing  →  200
     {
       "id": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
       "number": 1007,
       "displayNumber": "KM-1007",
       "status": "Preparing",
       "allowedActions": [
         "Dispatch",
         "Refund"
       ],
       "customerType": "guest",
       "deliveryCity": "Maputo",
       "paymentMethod": "MPesa",
       "paymentReference": "MP66949811",
       "lines": [
     …

15. …and hand it over: Shipping books a courier per seller (Adapter over two simulated couriers)
   POST http://localhost:5080/api/orders/c02a134f-18d6-454c-9ee8-f0d3ca7fd20f/transitions/Dispatch  →  200
     {
       "id": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
       "number": 1007,
       "displayNumber": "KM-1007",
       "status": "OutForDelivery",
       "allowedActions": [
         "Deliver"
       ],
       "customerType": "guest",
       "deliveryCity": "Maputo",
       "paymentMethod": "MPesa",
       "paymentReference": "MP66949811",
       "lines": [
         {
     …

16. Parcels and courier tracking (repeat after a few seconds: picked up → delivered)
   GET http://localhost:5080/api/shipping/consignments?orderId=c02a134f-18d6-454c-9ee8-f0d3ca7fd20f  →  200  (after 14 s)
     {
       "id": "ed18b66e-ff8e-4709-887d-83b32f518a30",
       "orderId": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
       "orderNumber": 1007,
       "method": "SameDay",
       "destinationCity": "Maputo",
       "district": "KaMpfumo",
       "pickupPointId": null,
       "status": "Delivered",
       "parcels": [
         {
           "id": "2b6b5e9c-f712-4051-a9b0-8f85146b8015",
           "reference": "KM-1007-2",
           "sellerId": "01920000-0000-7000-8000-000000000005",
     …

17. When the last parcel arrives the order is Delivered, and the customer got "a caminho" / "foi entregue" SMS
   GET http://localhost:5080/api/orders/c02a134f-18d6-454c-9ee8-f0d3ca7fd20f  →  200  (after 0 s)
     {
       "id": "c02a134f-18d6-454c-9ee8-f0d3ca7fd20f",
       "number": 1007,
       "displayNumber": "KM-1007",
       "status": "Delivered",
       "allowedActions": [
         "Refund"
       ],
       "customerType": "guest",
       "deliveryCity": "Maputo",
       "paymentMethod": "MPesa",
       "paymentReference": "MP66949811",
       "lines": [
         {
     …

18. Visitor: an issuer's e-Declaração file for the month, and its IVA summary
   (400100005 = Importadora Lebombo, a demo seller under IVA normal)
   GET http://localhost:5080/api/invoicing/e-declaracao/400100005/2026/10?format=csv  →  200
     NuitEmitente;TipoDocumento;NumeroDocumento;DataEmissao;NuitAdquirente;NomeAdquirente;DocumentoOrigem;CategoriaFiscal;Taxa;BaseTributavel;Iva;Total
     400100005;FT;FT 2026/000001;2026-10-18;999999999;Consumidor final;;standard;0.16;1245.78;199.33;1445.11
     

(continued)
   GET http://localhost:5080/api/invoicing/iva-summary/400100005/2026/10  →  200
     {
       "issuerNuit": "400100005",
       "year": 2026,
       "month": 10,
       "invoices": 1,
       "creditNotes": 0,
       "lines": [
         {
           "taxCategory": "standard",
           "rate": 0.1600,
           "net": 1245.78,
           "iva": 199.33,
           "gross": 1445.11
         }
     …

19. Singleton: the one cached IVA rate table, with its legal history (17 % before 2023)
   GET http://localhost:5080/api/invoicing/tax-rates?on=2022-06-01  →  200
     [
       {
         "category": "exempt",
         "rate": 0,
         "validFrom": "2008-01-01",
         "exemptionReason": "Isento de IVA nos termos do artigo 9.º do Código do IVA"
       },
       {
         "category": "standard",
         "rate": 0.17,
         "validFrom": "2008-01-01",
         "exemptionReason": null
       }
     ]

20. Operations: every module's outbox — pending messages and dead letters (replay after a fix)
   GET http://localhost:5080/api/operations/outbox  →  200
     [
       {
         "module": "Invoicing",
         "pending": 0,
         "deadLettered": 0,
         "oldestPendingAt": null
       },
       {
         "module": "Ordering",
         "pending": 1,
         "deadLettered": 0,
         "oldestPendingAt": "2026-10-18T12:06:18.884437+00:00"
       },
       {
     …

Template Method + Iterator: import the wholesaler's paginated catalog into a seller (streamed, page by page)
   POST http://localhost:5080/api/catalog/sellers/01920000-0000-7000-8000-000000000002/imports/wholesaler  →  200
     {
       "format": "WholesalerApi",
       "source": "wholesaler-api",
       "totalRows": 2400,
       "created": 0,
       "updated": 2400,
       "rejected": [],
       "fatalError": null,
       "accepted": 2400,
       "succeeded": true
     }

Command: an audited, undoable bulk price change (rand appreciation: +8 % for Importadora Lebombo)
   POST http://localhost:5080/api/catalog/backoffice/sellers/01920000-0000-7000-8000-000000000005/price-adjustments  →  200
     {
       "id": "01a14ee7-fc7c-7915-b342-044854dd02bc",
       "kind": "adjust-seller-prices",
       "description": "Adjust prices of seller 01920000-0000-7000-8000-000000000005 by +8% (2 products): Rand appreciation",
       "performedBy": "ana.backoffice",
       "performedAt": "2026-10-18T12:06:28.7320794+00:00",
       "status": "Executed",
       "undoneBy": null,
       "undoneAt": null
     }

(continued)
   GET http://localhost:5080/api/catalog/backoffice/operations  →  200
     {
       "items": [
         {
           "id": "01a14ee7-fc7c-7915-b342-044854dd02bc",
           "kind": "adjust-seller-prices",
           "description": "Adjust prices of seller 01920000-0000-7000-8000-000000000005 by +8% (2 products): Rand appreciation",
           "performedBy": "ana.backoffice",
           "performedAt": "2026-10-18T12:06:28.732079+00:00",
           "status": "Executed",
           "undoneBy": null,
           "undoneAt": null
         },
         {
           "id": "01a14b98-7a45-7846-9329-17e195da3884",
     …

Memento: the cart's snapshots, and undo of the last change
   GET http://localhost:5080/api/carts/17e3f831-327f-4aea-b420-a53c136b376d/snapshots  →  200
     [
       {
         "version": 1,
         "takenAt": "2026-10-18T12:05:55.0497638+00:00",
         "deliveryCity": "Maputo",
         "itemCount": 0,
         "lines": []
       },
       {
         "version": 2,
         "takenAt": "2026-10-18T12:05:55.6705513+00:00",
         "deliveryCity": "Maputo",
         "itemCount": 1,
         "lines": [
     …

Interpreter: check a promotion rule while marketing types it (errors point at the position)
   POST http://localhost:5080/api/promotions/rules/validate  →  200
     {
       "isValid": false,
       "canonicalRule": null,
       "error": {
         "position": 20,
         "length": 2,
         "message": "Unexpected 'AN': expected AND, OR or end of rule.",
         "excerpt": "cart.total >= 2500 AN customer.city = \"Maputo\"\n                   ^^"
       }
     }

All 26 requests succeeded.
```
