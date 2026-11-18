# 11 — Pipeline view and submitting a draft

## Goal

The Order List gets its third layout, the mockup's Pipeline: the orders of the current view as a
board with one column per status. The one move the mockup gives the client — a draft dragged to
Ready — is real: it submits the draft through the domain, with the same concurrency rule as
recording an operation. The brief names the pipeline view as optional; this step is beyond it.

## Decisions

- **One endpoint feeds the board.** `GET /api/v1/orders/pipeline` returns every column with its
  total and its first cards. The page could call the list four times instead, but then it would
  decide which statuses exist and in what order, and make four requests where one answers. The
  totals and the cards are still two statements, as in the list: a submit that lands between
  them can leave a column one card ahead of its total until the next load.
- **Four columns, one per `OrderStatus`.** The mockup's note lists seven (Draft · Ready · In
  Progress · Consolidated · In Transit · Deconsolidated · Closed); three of them are stages the
  model does not have, and the mockup analysis already records that the note contradicts the
  four-state status flow. Columns are the statuses the domain owns, in enum order. A card keeps its
  "Next" line, which tells the stage within In Progress.
- **The board takes the filter of the list**, `status` included: tabs, Hub, Date, Status and search
  work as in the other layouts. A column whose status the filter excludes is returned empty with a
  total of 0, so the board never changes shape.
- **Counting and picking the first cards happen in the database**: the totals with one `GROUP BY`,
  the first `PipelineColumnSize` (5) orders of each status with a window function, and the cards of
  those orders through the card projection the list already uses (`OrderCardReader`). The order
  inside a column is the list's order.
- **Only Draft → Ready is a move of the client.** Every other transition is the hub's work and is
  driven by operations, so only draft cards can be dragged and only the Ready column takes them.
  Dragging is not reachable by keyboard or touch, so a draft card on the board also has a
  **Submit** button that does the same.
- **Submitting is `Order.Submit`.** The handler loads the order by its number, asks the order
  whether it can be submitted, takes the next official number, calls `Submit`, and saves under
  the optimistic concurrency of the write flow
  ([ADR 0013](../adr/0013-optimistic-concurrency-on-order-version.md)). A draft that lacks what
  `Submit` requires is refused with the domain's own message, which names what is missing.
- **Official numbers come from a PostgreSQL sequence**, `FR` followed by six digits
  ([ADR 0016](../adr/0016-official-order-numbers-from-a-sequence.md)). A refused submit takes no
  number; a submit that loses a race to another leaves a gap, which is accepted — the numbers
  are unique and increasing, not dense. The migration creates the sequence and starts it after
  the highest official number already stored; the seeder does the same after a rebuild is
  committed, so a rebuild starts from the same number every time.
- **The sample data gains one draft that can be submitted.** `DRAFT-003` stays as it is — nothing
  but a date, so it shows the refusal. `DRAFT-004` is complete, so it shows the move.

## Contract

### `GET /api/v1/orders/pipeline`

Query: the filters of `GET /api/v1/orders` — `type`, `status`, `hasOpenAlerts`, `hubId`,
`datePreset`, `search` — with the same validation and the same `400`. No `page`, no `pageSize`.

```json
{
  "columns": [
    { "status": "Draft", "total": 2, "orders": [ /* OrderCardDto, at most 5 */ ] },
    { "status": "Ready", "total": 2, "orders": [] },
    { "status": "InProgress", "total": 5, "orders": [] },
    { "status": "Completed", "total": 62, "orders": [] }
  ]
}
```

Always the four columns, in this order. `orders` holds the same card as an item of the list.

### `POST /api/v1/orders/{number}/submit`

Body: `{ "expectedVersion": 3 }` — the version the caller saw, the same field and rule as in
`POST /api/v1/orders/{number}/operations`.

- `200` — `{ "number": "FR001682", "status": "Ready", "version": 4 }`. The number is the new,
  official one; the draft number no longer names the order.
- `400` — the body is malformed or `expectedVersion` is missing or not positive.
- `404` — no order with this number.
- `409` — the version is stale; or the order is not a draft; or the draft is not ready to submit
  (the detail lists what is missing). The problem `code`s are the existing ones of the write flow
  and of `OrderErrors`.

### Routes

`ApiRoutes.Orders.Pipeline = "orders/pipeline"`, `ApiRoutes.Orders.Submit =
$"orders/{{{NumberParameter}}}/submit"`. Endpoint folders `Orders/GetPipeline` and
`Orders/SubmitOrder`; application folders the same.

### Schema

One additive migration: the sequence `order_number_seq`, set to the highest numeric part of the
official numbers already stored (the sequence's own start when there is none). No table changes.
The application reaches it through a port, `IOrderNumbers.NextAsync`, implemented in
Infrastructure.

A second additive migration adds the index `(company_id, status, scheduled_at DESC, id DESC)`.
On the volume set it turns both board statements into index-only scans and removes the
window's sort; the plans before and after are in
[`docs/explain/pipeline.md`](../explain/pipeline.md).

### Sample data

| Order | Script | State |
|---|---|---|
| **DRAFT-004** · Cross-Dock Storage · Toronto · User 2 | Draft `D−1 17:00`: REF-1016, 12 Std; scheduled `D+4 11:00`; London, ON; carrier Schneider. Not submitted | Draft; ready to submit |

Totals after this step: 71 orders — 2 Draft, 2 Ready, 5 In progress, 62 Completed; tabs All 71,
Cross-Dock 52, Consolidation 19, Alerts 2, Drafts 2. A draft awaits the client, so the
dashboard's Need attention and the Orders badge go from 4 to 5 (3 awaiting action, 2 alerts);
Active Orders and Completed do not change. The
sequence stands at the highest seeded official number after every rebuild.

### Page (`orders.html`)

- The layout switch has three options: Cards, Table, Pipeline (`view=pipeline` in the URL, kept
  across tabs, filters and search like `view=table`). The mockup's Pipeline toggle is used; its
  preview note stays hidden.
- The board shows the four columns side by side, each with its status label, its total and its
  cards — a compact form of the list card: number (a link to the order), type, hub, scheduled
  date, declared and actual totals, alert marks, the "Next" line. A column with more orders than
  cards ends with "+N more", a link to the Cards layout filtered to that status (the Drafts tab
  for Draft). An empty column says so.
- There is no pager on the board; the "Showing" text gives the number of orders on the board.
- A draft card is draggable and has a Submit button. While a draft is dragged the Ready column is
  marked as the drop target; a drop anywhere else does nothing. The request sends the card's
  version, so the card DTO must carry it — add `version` to `OrderCardDto` if it is not there.
- After a submit: a toast with the new number ("FR001682 is ready"), then the board, the tab
  counters and the Orders badge are loaded again. A refusal shows the server's detail in the toast
  and changes nothing; a `409` for a stale version reloads the board.
- Everything else of step 7 holds: the view lives in the URL, changes are made in place through
  the History API, a stale response is dropped, `aria-busy` while loading, no inline script.

## Acceptance criteria

- [ ] `GET /api/v1/orders/pipeline` on the seeded data returns four columns with totals 2, 2, 5,
      62 and at most five cards each, in the list's order.
- [ ] With `type=Consolidation`, `hasOpenAlerts=true`, a hub, a date preset or a search, each
      column's total equals the `total` of `GET /api/v1/orders` with the same filter and that
      status; with `status=Ready` only the Ready column is filled.
- [ ] `POST /api/v1/orders/DRAFT-004/submit` with the current version answers `200` with a number
      `FR` + six digits greater than every seeded number; the order is then found under the new
      number with status Ready, and no longer under `DRAFT-004`.
- [ ] The same request for `DRAFT-003` answers `409` and names a destination, a transport
      arrangement and the two shipments of a Consolidation; for a submitted order `409` "not a
      draft"; with a stale version `409`; for an unknown number `404`.
- [ ] Two concurrent submits of the same draft: one `200`, one `409`, one official number used by
      an order.
- [ ] On `orders.html?view=pipeline` the four columns appear; dragging `DRAFT-004` to Ready (or
      its Submit button) moves it, and the Drafts counter, the column totals and the dashboard's
      Active Orders follow; `DRAFT-003` is refused with the list of what is missing.
- [ ] After a rebuild of the sample data the same submit yields the same number again.
- [ ] The OpenAPI document describes both endpoints with their responses; `README.md`,
      `docs/plan.md` and `docs/steps/03-seed.md` list the endpoints and the new totals.

## Tests

- The board: totals and first cards per status, the list's order inside a column, every filter,
  an excluded status returned empty, validation errors — integration.
- Submit: the happy path with the number replaced, not ready to submit, not a draft, stale
  version, unknown order, a malformed body, two concurrent submits — integration.
- The sequence: starts after the highest official number after seeding and after a rebuild;
  numbers are `FR` + six digits — integration.
- The seeded totals (71 orders, the tabs, Need attention 5) — integration, by updating the
  existing seed tests.
- `Order.Submit` is already covered by the domain tests; add only what is missing.

## Out of scope

- Columns by stage (Awaiting truck, Unloading, Loading, Awaiting completion). The stage is derived
  in memory from the shipments; grouping by it in SQL would state the rule twice.
- Any other move between columns, roles, and creating or editing a draft.
- Reordering cards, column limits, saved boards.
