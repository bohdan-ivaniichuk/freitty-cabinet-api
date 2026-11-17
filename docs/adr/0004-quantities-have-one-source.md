# 0004 — Every quantity has one source; nothing derived is stored

- Status: accepted
- Date: 2026-11-17

## Context

The mockup shows what happens when a quantity lives in several places. FR001676 says
"15 Std + 3 XL" on the card and lists sub-orders of 9, 6 and 12 pallets; the toolbar of FR001383
counts no photos while its operations show seven. The first draft of the model repeated the pattern:
a declared and an actual quantity on the order, cargo lines on the order, and a pallet count on each
shipment.

The business reads these numbers per shipment. A bill of lading has lines with different handling
units, handling is priced per unit type, and a shortage is settled shipment by shipment — so a
difference has to be visible per shipment and per unit type, not as one total.

## Decision

- **Declared** quantity lives only in Cargo Lines: one line per Unit Type of a Shipment, as
  on the bill of lading. The quantity of a shipment or of an order is a sum of lines.
- **Actual** quantity lives only in the Operations ledger. Each Operation belongs to a Shipment and
  has a Unit Type and a quantity. Actual quantity is the sum of Unloading; On stock is unloaded minus
  disposed minus loaded.
- The Quantity delta, order totals, trailer counts, the order's photo and comment totals and supply
  totals are computed — in the aggregate for its rules, in SQL for the screens. None has a column.
- An Alert is the exception that is not one: it is stored, because it records when the client was
  told and when the matter ended. That history cannot be recomputed from current quantities.

## Alternatives

- **One pallet type per shipment, no Cargo Lines** — one table fewer and it fits the mockup data.
  Rejected: a shipment with mixed units would have to be split into two shipments with the same
  Ref N, and a per-line difference (8 Std + 2 XL against 10 Std) would be invisible.
- **A separate receiving count on the Cargo Line** — reads faster. Rejected: an Unloading of 12 and
  a count of 11 would be two truths with no rule to choose between them.
- **A stored actual total on the order, recalculated on every operation** — a cheap list query and a
  natural column for a concurrency token. Rejected: it is a second truth beside the ledger, and one
  number per order cannot express a difference per line.
- **Operations attached to the order only** — simpler. Rejected: in a Consolidation it could not say
  which shipment arrived short.

## Consequences

- The contradictions of the mockup cannot be reproduced: there is no second number to disagree.
- Two stored numbers are not derived from anything here and are kept as given: a company's balance
  (top-ups and payments are outside the task) and the photo and comment counters of an operation
  (files and chat are outside it too).
- The computed members of the aggregate are for its rules only. A read query that used them would
  get zeros without an error, because it loads no children; the screens repeat the arithmetic in
  SQL.
- The list query computes sums over Cargo Lines and Operations for the orders of a page. That is a
  bounded cost and the kind of work SQL should do.
- The ledger alone does not know when receiving is over, so a shortage cannot be read from it while
  freight is still arriving. One explicit fact is stored for that — the instant the hub finished
  receiving a shipment. An excess needs no such fact: it is true as soon as it is unloaded.
- One Cargo Line per Unit Type per shipment: a Cargo Line has no commodity, so two lines of the
  same type would be indistinguishable. A commodity column lifts the restriction.
- Concurrency control cannot lean on a recalculated column. The root carries a version counter
  that every change increments, so the order row is written by every change and the write flow has
  somewhere to put its token.
