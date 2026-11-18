# 05 — Order Detail

## Goal

`GET /api/v1/orders/{number}` returns one order with everything the detail page shows: its header,
its shipments with cargo, arrival, dock, declared against actual and the operations ledger, its
supplies with totals, services, alerts and invoices. It is a read: nothing loads the aggregate.

## Decisions

- **One endpoint, one read handler, projections only** — `AsNoTracking`, `Select` to DTOs, no
  entity materialised. The reading is `OrderDetailReader`, a service of Application registered in
  `AddApplication` next to `OrderCardReader`; it returns `null` when the company has no order with
  that number. The handler only resolves the current user, calls the reader and turns `null` into
  the not-found error. It is built on what [step 4](04-order-list.md) wired: FastEndpoints, the
  pipeline, `ProblemDetails`, `IAppDbContext`, `ICurrentUser`, the reporting calendar.
- **Split queries.** The order has four collections two levels deep; one joined statement would
  multiply them. The reader issues a small, fixed number of statements — the order row with its
  references, then the collections — with `AsSplitQuery` where one projection carries collections.
  No statement returns the product of two collections. The statements share no snapshot, so a write
  committed between them can show in part; that is accepted because `version` is read in the first
  statement, and a write decided on a stale page gets `409`.
- **Found by number within the current company.** An unknown number and another company's number
  are the same `404`; the message names the number. The handler returns
  `Result<OrderDetailDto>`; "not found" is a `DomainError` of kind `NotFound` with the code
  `order.not_found`, created in Application — the aggregate cannot refuse an order that was never
  loaded. The endpoint turns it into a `ProblemDetails` with the `DomainError` mapper, which
  arrives in this step and is reused by step 8.
- **A draft is returned like any other order.** The list links to it ("Continue editing"), and a
  card that opens nothing is worse than a page with dashes. A draft has empty collections and
  `null` where it has no value yet.
- **One template for both types.** A Cross-Dock is the case of one shipment; a Consolidation
  returns one block per shipment, each with its own arrival, dock, quantities, alerts and ledger.
- **Quantities come from the shared read arithmetic of step 4** (`ShipmentQuantities`): sums
  grouped in SQL, the subtraction in one class. The computed members of the aggregate are never
  used in a projection — they return zero there ([step 2](02-domain.md)).
- **Progress is a code, not a sentence.** `stage` describes the freight and ignores alerts; the
  page words it. It comes from the same `OrderProgress` class that gives a card its `nextStep` line
  in step 4, and uses the same names for the same conditions.
- **Instants are UTC; the hub's IANA zone travels with them.** The page formats hub-local time.
- **`version` is part of the response** — the write flow of [step 8](08-write-flow.md) sends it
  back.

## Contract

### `GET /api/v1/orders/{number}`

`{number}` is matched as a whole, case-insensitively (`fr001383` finds `FR001383`):
`lower(number) = @number`, the parameter lower-cased by the application, never a pattern match — a
`_` in the route is not a wildcard. The detail and the write of step 8 find the order through the
same query extension, `OrderQueries.WithNumber`.

The example is FR001383 as the seed leaves it, instants shortened:

```jsonc
{
  "number": "FR001383",
  "type": "CrossDock",                  // CrossDock | Consolidation
  "crossDockKind": "Storage",           // null for a Consolidation
  "status": "InProgress",
  "stage": "Loading",                   // see below
  "version": 20,
  "createdBy": { "name": "User 2", "initials": "U2", "role": "Dispatcher" },
  "company": { "name": "R-way Transport" },
  "hub": { "name": "Markham", "shortName": "Mark", "region": "ON", "timeZone": "America/Toronto" },
  "transferHub": null,                  // { "name", "shortName" } when set
  "scheduledAt": "2026-04-17T12:30:00+00:00",
  "destination": { "city": "Kingston", "region": "ON" },      // null in a draft
  "transport": { "kind": "Carrier", "carrierName": "R-way Transport Inc.",
                 "carrierPhone": "+1 647 555 0199", "driverName": null },   // null in a draft
  "services": ["Restock & Rework", "Transload"],
  "totals": {
    "declared": [{ "unitType": "Standard", "quantity": 10 }],
    "actual":   [{ "unitType": "Standard", "quantity": 12 }],
    "onStock":  [{ "unitType": "Standard", "quantity": 5 }],
    "quantityDeltaCount": 1,            // (shipment, Unit Type) pairs with a known delta
    "photoCount": 7, "commentCount": 1, // sums over all operations
    "outboundTrailerCount": 1           // distinct trailer numbers of Loadings
  },
  "shipments": [{
    "position": 1,
    "label": "FR001383-1",
    "refNumber": "REF-1012",
    "receivedAt": "2026-04-17T13:05:00+00:00",
    "warehouseNote": "Counted 12 pallets on arrival, …",
    "arrival": { "trailerType": "DryVan53", "truckNumber": "TRK-4521",
                 "trailerNumber": "TRL-8830", "arrivedAt": "…" },           // null before arrival
    "dock": { "dock": "12", "bay": "B", "floorLead": "User 6", "assignedAt": "…" },  // or null
    "quantities": [{ "unitType": "Standard", "declared": 10, "actual": 12,
                     "delta": 2, "onStock": 5 }],                           // delta: null if not known
    "alerts": [{ "id": "…", "type": "QuantityDelta", "raisedAt": "…", "resolvedAt": null }],
    "operations": [{ "id": "…", "type": "Unloading", "unitType": "Standard", "quantity": 12,
                     "trailerNumber": "TRL-8830", "appliedAt": "…", "isBillable": true,
                     "photoCount": 4, "commentCount": 0 }]
  }],
  "supplies": {
    "lines": [{ "sku": "Straps 12", "category": "Securement", "quantity": 4,
                "unitPrice": 18.50, "lineTotal": 74.00 }],
    "subtotal": 314.00
  },
  "invoices": []       // items: { "number": "001812", "amount": 280.00, "issuedAt": "…", "paidAt": "…" | null }
}
```

Rules:

- Order: `shipments` by `position`; `operations` by `appliedAt`, then `id`; `alerts` by `raisedAt`,
  then `type`, then `id`; `services` by name; `supplies.lines` by category, then SKU, then `id`;
  `invoices` by `issuedAt`, then `number`. Ids are UUID v7 and do not follow creation within a
  millisecond ([ADR 0006](../adr/0006-uuid-v7-keys-from-the-domain.md)), so `id` is only ever a
  tie-break that keeps the order stable; tests do not assert the order of rows that tie.
- `quantities` has one row per Unit Type that the shipment declares or has an Operation for,
  `Standard` first. `delta` is `actual − declared` when it is positive, or when the shipment is
  received (then it may be zero or negative); otherwise `null` — the definition of
  [`CONTEXT.md`](../../CONTEXT.md) and of `Shipment.HasQuantityDelta`.
- `totals.declared`, `actual` and `onStock` have one entry per Unit Type that appears in any
  shipment's `quantities`, zeros included, `Standard` first. `totals.quantityDeltaCount` counts
  the rows of `quantities`, over all shipments, whose `delta` is neither `null` nor zero.
- A draft without shipments has `shipments: []`, the three `totals` arrays empty, the counters 0,
  `supplies.lines` empty and `supplies.subtotal` 0.
- `lineTotal` and `subtotal` are multiplied and summed in SQL.
- `label` is `<number>-<position>` for every shipment, also the single one of a Cross-Dock.
- Money is a JSON number with two decimals; enums are strings (the converter of step 4).

**`stage`** — from `OrderProgress`, the first that applies:

| Stage | When |
|---|---|
| `Draft` | status `Draft` |
| `Completed` | status `Completed` |
| `AwaitingTruck` | no shipment has arrived, none has an Operation and none is received |
| `Unloading` | some shipment is not received |
| `Loading` | every shipment is received and something is On stock |
| `AwaitingCompletion` | every shipment is received and nothing is On stock |

The last four are the conditions of the same names in the `nextStep` table of step 4.

### Errors

| Case | Response |
|---|---|
| Unknown number, or an order of another company | `404` `ProblemDetails` with `code` `order.not_found` and the detail `Order fr009999 not found.` — the number as the route gave it |

The number comes from the route alone: a `number` in the query string does not replace it.

### Code

```
src/Freitty.Application/Orders/GetOrderDetail/   GetOrderDetailQuery, its handler, OrderDetailReader,
                                                 OrderDetailDto and its parts
src/Freitty.Application/Orders/                  OrderRequestErrors — the application's own errors
                                                 (order not found); OrderQueries.WithNumber
src/Freitty.Api/Endpoints/Orders/GetOrderDetail/ the endpoint
src/Freitty.Api/Errors/                          DomainErrorProblem (DomainError → ProblemDetails),
                                                 DomainProblemDetails (ProblemDetails plus `code`),
                                                 its OpenAPI description DomainProblemDescription
```

The query has nothing to validate beyond the route value being present, so it has no validator.
`Freitty.Application` gains `Microsoft.EntityFrameworkCore.Relational` for `AsSplitQuery` —
relational, not a provider, so Application still cannot see Npgsql. The error codes are public
constants (`OrderRequestErrors.OrderNotFoundCode`), and the endpoint's description interpolates
them.

## Acceptance criteria

- [ ] FR001383 from the seed returns the example above: declared 10, actual 12, delta 2, On
      stock 5, stage `Loading`, version 20, the four operations Unloading, Disposal, Restack,
      Loading, photos 7, comments 1, three supply lines with subtotal 314.00, no invoice, one open
      Quantity delta alert.
- [ ] FR001674 returns two shipments; the second has both Alerts open, declared 9, actual 7,
      delta −2.
- [ ] DRAFT-003 returns `200` with stage `Draft`, no shipments, `destination` and `transport`
      `null`.
- [ ] With `Microsoft.EntityFrameworkCore.Database.Command` at `Information`, one request logs
      several `SELECT`s, and none returns the product of two sibling collections. The handler
      uses `AsNoTracking` and loads no entity.
- [ ] Scalar shows the endpoint with its response schema; enums are strings.
- [ ] Build with 0 warnings, `dotnet format --verify-no-changes` clean, `dotnet test` green.

## Tests

Integration, through HTTP, in `Freitty.Api.IntegrationTests/Orders/`. Each test builds its orders
through the aggregate under its own company, with the builders step 4 added; a builder this step
needs and does not find — a Consolidation, a completed order, an event at a chosen instant — is
added to them. Money is compared as a number, not as text.

- The detail of an order with a known ledger: per-shipment and total quantities, delta, On stock,
  photo and comment sums, outbound trailers, supply line totals and subtotal.
- A shortage shows a `null` delta before the shipment is received and a negative one after.
- A Unit Type that was unloaded but not declared gets its own `quantities` row with declared 0.
- A Consolidation with shipments in different states returns each with its own arrival, dock,
  alerts and operations.
- Operations come back in recorded order; open and resolved alerts both come back.
- Each `stage` of the table.
- A draft returns `200` with empty collections.
- An unknown number and an order of another company → `404` with `order.not_found`, naming the
  number; a `_` in the number matches no other order.
- The number is matched case-insensitively.
- Parity: for an order with operations, alerts and supplies, the quantities, On stock and supply
  subtotal of the response equal the computed members of the aggregate loaded from the database.

## Out of scope

- The write flow behind "+ Operation" — step 8. "+ Supply", "Edit", "Share QR", "BOL PDF", chat.
- The page itself — [step 7](07-frontend.md).
- Photo thumbnails, the denominators of the toolbar counters, the neighbouring docks: nothing in
  the model produces them (step 2, *Out of scope*).
