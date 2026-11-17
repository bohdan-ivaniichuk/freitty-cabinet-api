# 05 — Order Detail

> **Draft, not groomed.** Written ahead of the grooming session so it does not start from zero.
> Everything under *Decisions* is a proposal; *Open questions* is what the session has to settle.
> Nothing here is input for implementation yet.

## Goal

The Order Detail screen gets one order from the API with everything the page shows: its shipments
with cargo, arrival, dock, declared against actual, the operations ledger, supplies with totals,
services and alerts.

## Decisions

- One endpoint, one read handler, a projection to a DTO with `AsNoTracking`. The order has several
  collections, so the query uses `AsSplitQuery` — the plan's example of avoiding a cartesian
  product.
- The order is found by its number within the current company. An unknown number is a `404`
  `ProblemDetails` that names it.
- **One template for both types.** Only a Cross-Dock detail is drawn. A Consolidation opens in the
  same layout with a block per shipment; a Cross-Dock is the case of one block
  (`mockup-analysis.md`, open question 1).
- Everything shown as a number is computed in SQL from `cargo_lines` and `operations`; the computed
  members of the aggregate are not used in the projection.
- Presentation states are sent as codes with their data; the page owns the wording.

## Contract

### `GET /api/v1/orders/{number}`

- **Header** — number, type, Cross-Dock kind, status, stage (see open question 1), who created it,
  whether a Quantity delta is known, counters: photos and comments summed over operations.
- **Meta** — customer (the company), hub and region, services, Scheduled date, declared quantity
  per Unit Type, actual quantity and delta, transport (kind, carrier name and phone, or driver),
  destination, transfer hub, the status flow with the current step, number of deltas.
- **Shipments**, each with:
  - sub-order label, Ref N, received or not;
  - arrival: trailer type, truck and trailer number, arrived at;
  - dock: dock, bay, floor lead, assigned at;
  - per Unit Type: declared, actual, delta, on stock;
  - warehouse note;
  - open and resolved alerts;
  - operations in the order they were recorded: type, billable, trailer, quantity, Unit Type,
    applied at, comment and photo counters.
- **Supplies** — SKU, category, quantity, unit price, line total; subtotal.
- **Invoices** — number, amount, issued at, paid at.

### Errors

Unknown number → `404`. A draft → see open question 4.

## Acceptance criteria

- [ ] FR001383 from the seed returns every value of the mockup's detail page, with the
      normalisations of step 3.
- [ ] A Consolidation returns one block per shipment with its own arrival, dock and ledger.
- [ ] The SQL log shows split queries and no entity materialised for mapping.
- [ ] Scalar shows the endpoint with its schema.

## Tests

- The detail of an order with a known ledger: quantities, deltas, on stock, counters, totals —
  integration
- A Consolidation with shipments in different states — integration
- An unknown number and an order of another company → `404` — integration
- Operations come back in recorded order; alerts split into open and resolved — integration

## Out of scope

- The write flow behind "+ Operation" — step 8. "+ Supply", "Edit", "Share QR", "BOL PDF", chat.
- The page itself — step 7.

## Open questions

1. **"● On Stock" and "Loading in progress".** Two badges next to the type. Proposal: the first is
   shown while anything is On stock; the second is derived from the last operation ("Unloading in
   progress", "Loading in progress"). What do they say before the truck arrives and after
   everything is loaded?
2. **"Deltas detected: 1 (q-ty)".** The number of Unit Types with a delta, of shipments with a
   delta, or of Quantity delta alerts?
3. **The photo thumbnails beside the warehouse note** ("+3"). Proposal: the photo count of the
   shipment's Unloadings. Or drop them, since there are no files?
4. **A draft.** The list's "Continue editing" leads to a builder that does not exist. Does the
   detail endpoint return a draft, or `404`?
5. **The toolbar counters** show "0/5" and "0/0". The numerators are sums over operations; the
   denominators have no source. Show only the numerator?
6. **"Trailer docked · loading"** on the dock card. Proposal: shown from arrival until the order is
   completed. Good enough?
7. **A Consolidation's detail layout** is not drawn. How much of it does step 7 build: the same
   page with repeated blocks, or tabs per shipment?
8. **The green "$" and truck icons** after the order number: what do they mean — an invoice exists,
   transport is arranged?
9. **Hub-local time**: the same answer as in step 4, question 10.
