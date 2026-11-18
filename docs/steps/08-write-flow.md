# 08 — Write flow

## Goal

One write through the API: recording an Operation on a shipment, from a form on the detail page.
The task does not ask for it; it is there to show a transaction, optimistic concurrency and the
domain rules at work — the ledger grows, the order starts, a Quantity delta alert opens or
resolves, all or nothing.

## Decisions

- **One endpoint.** `POST /api/v1/orders/{number}/operations`. Finishing receiving, resolving an
  Alert and completing an order stay in the domain without an endpoint; the seed of
  [step 3](03-seed.md) prepares the stages this one write needs.
- **The handler orchestrates, the aggregate decides** (rule 1): find the order, check the version,
  resolve the shipment, call `RecordOperation`, save. The validator checks the shape of the
  request and nothing about the state of the order.
- **One `SaveChangesAsync`, one transaction.** The Operation, the Alert it raises or resolves, the
  status change and the order row are written together or not at all.
- **Optimistic concurrency on `Order.Version`** —
  [ADR 0013](../adr/0013-optimistic-concurrency-on-order-version.md). `Version` becomes the EF Core
  concurrency token; every successful method of the aggregate already increments it, so the order
  row is written by every change. `READ COMMITTED` stays; no row lock, no `SERIALIZABLE`.
- **The caller says which version it saw.** The request carries `expectedVersion`; a stale page, a
  double click and a client retry all get `409` instead of appending twice. An Unloading cannot be
  taken back, so a repeated request must not be recorded.
- **A lost race has two faces, both `409`.** `DbUpdateConcurrencyException` on the order row, and a
  unique violation on the open-alert index when two writers raise the same Alert — EF Core inserts
  the Alert before it updates the order, so the index answers first. Both are recognised where the
  provider is known: `IAppDbContext` gains `Task<bool> TrySaveChangesAsync(cancellationToken)`,
  `false` meaning "another writer changed the same order first"; `AppDbContext` implements it by
  catching exactly those two failures. The violation is matched by `SqlState` `23505` and the name
  of that index, which is pinned with `HasDatabaseName` to the name it already has
  (`ix_alerts_shipment_id_type`) through one constant. Application never sees Npgsql, and the
  handler turns `false` into a refusal, not an exception (rule 6).
- **No retry inside the request.** A `409` goes back to the caller, who reloads and decides.
- **A refusal is a `Result`, never an exception.** It becomes a `ProblemDetails` through the
  mapper of step 5; the HTTP status follows `DomainError.Kind` — `NotFound` → `404`, `Conflict` →
  `409` — and the stable code travels in the `code` extension. The refusals the aggregate cannot
  make — order not found, no shipment at that position, version conflict — are the application's
  own errors, next to `order.not_found` of step 5.
- **The aggregate is loaded with split queries**, tracked: the order with its Shipments and their
  Cargo Lines, Operations and Alerts — without the Cargo Lines every delta would be computed
  against a declared quantity of zero. Supply lines and Services are not needed and not loaded.
  The order row is read first, so if another writer commits between the statements, the version
  read is already stale and the save fails.
- **The instant comes from `TimeProvider`.** The seed dates nothing later than the day before it
  ran, so `now >= UpdatedAt` holds for every seeded order.
- **Mapping is Mapperly** (rule 4): the request to the command in Api, the command to
  `NewOperation` in Application. The build treats warnings as errors, so the members without a
  counterpart are declared: `CommentCount` is a constant zero (`[MapValue]`), and the command's
  order number, shipment position and expected version are ignored as sources
  (`[MapperIgnoreSource]`).

## Contract

### `POST /api/v1/orders/{number}/operations`

`{number}` is matched as in step 5: the whole number, case-insensitively, within the current
company.

```jsonc
{
  "shipmentPosition": 1,          // the position shown on the page, from 1
  "type": "Loading",              // Unloading | Disposal | Restack | Loading
  "unitType": "Standard",         // Standard | ExtraLarge
  "quantity": 5,
  "trailerNumber": "TRL-8830",    // required for Unloading and Loading; null or absent otherwise
  "isBillable": true,             // optional, false when absent
  "photoCount": 0,                // optional, 0 when absent
  "expectedVersion": 20
}
```

`commentCount` is not part of the request: there is no chat, so it is recorded as zero.

Response `201`, `Location: /api/v1/orders/{number}`:

```jsonc
{
  "operation": { "id": "…", "type": "Loading", "unitType": "Standard", "quantity": 5,
                 "trailerNumber": "TRL-8830", "appliedAt": "…", "isBillable": true,
                 "photoCount": 0, "commentCount": 0 },
  "order": { "number": "FR001383", "status": "InProgress", "version": 21 }
}
```

The page refetches the detail after a `201`; the response carries only what identifies the result.

**Validation** — `400` with the field errors. Every rule below keeps a value from reaching the
aggregate that it would reject as a broken contract, which would be a `500`:

| Field | Rule |
|---|---|
| `shipmentPosition` | required; 1 or more |
| `type`, `unitType` | required; a defined member of the enum (`IsInEnum`) — a number outside it passes the JSON converter |
| `quantity` | required; 1 to 10 000, a named limit: the sums of the ledger are `int`, and the endpoint is public |
| `trailerNumber` | for Unloading and Loading: not empty or white space, at most 20 characters. For Disposal and Restack: must be `null` or absent — an empty string is refused |
| `photoCount` | 0 to 99 |
| `expectedVersion` | required; 1 or more |

An unknown enum name or a malformed number never reaches the validator: the binder refuses it
with the same `400`.

**Then, in this order:**

| Check | Status | `code` | Detail |
|---|---|---|---|
| The order exists in the current company | `404` | `order.not_found` | `Order FR009999 not found.` |
| `expectedVersion` equals the order's version | `409` | `order.version_conflict` | `Order FR001383 was changed since you loaded it (version 21, expected 20). Reload and try again.` |
| The order has a shipment at that position | `404` | `shipment.not_found` | `Order FR001674 has no shipment at position 5.` |
| `RecordOperation` accepts it | `409` | the `DomainError` code: `order.not_active`, `operation.exceeds_stock` | the aggregate's message |
| `TrySaveChangesAsync` returns `true` | `409` | `order.version_conflict` | `Order FR001383 was changed while this request was being processed. Reload and try again.` |

So a draft without shipments answers `404` for any position, and a draft that has the shipment
answers `409` `order.not_active`.

### Schema

None. A concurrency token is not DDL: `Version` is marked `IsConcurrencyToken()` in
`OrderConfiguration`, `dotnet ef migrations has-pending-model-changes` reports nothing, and no
migration is added. The snapshot is not edited by hand. Naming the Alert index with
`HasDatabaseName` to the name it already has changes nothing either; the existing migration test
proves both.

### Code

```
src/Freitty.Application/Orders/RecordOperation/   RecordOperationCommand, its handler and validator,
                                                  the result DTO, the Mapperly mapper to NewOperation
src/Freitty.Application/Common/                   the three application errors
src/Freitty.Api/Endpoints/Orders/RecordOperation/ the endpoint, its request record, the Mapperly
                                                  mapper to the command
src/Freitty.Infrastructure/Persistence/           IsConcurrencyToken in OrderConfiguration; the
                                                  Alert index name as one constant;
                                                  TrySaveChangesAsync in AppDbContext
```

`Location` carries the order's stored number, not the route's spelling.

`Directory.Packages.props` gains `Riok.Mapperly`, the latest stable version, looked up; it is a
source generator, referenced with `PrivateAssets="all"` and `ExcludeAssets="runtime"` by the two
projects that map.

### The form

On `order.html` of [step 7](07-frontend.md) every "+ Operation" button — one per shipment block —
opens one `<dialog>`. It uses the mockup's `.field` and `.field-row` classes, which stay in
`app.css` for this, and `btn` / `btn-primary`. The dialog element sits outside the region the
page re-renders after a refetch.

The mockup's reset (`* { margin: 0; padding: 0 }`) removes the browser's centring and padding of a
dialog, and `.field input { width: 100% }` stretches a checkbox, so the marked block of `app.css`
gains: `dialog { margin: auto; padding: 20px; border: 1px solid #E5E7EB; border-radius: 10px;
width: min(440px, 90vw) }`, `dialog::backdrop { background: rgba(31, 42, 58, .4) }`,
`.field input[type=checkbox] { width: auto }`, and `.field-error` (12px, `#DC2626`).

| Field | Control | Default |
|---|---|---|
| Sub-order | a select of the sub-order labels; hidden when the order has one shipment | the shipment whose button was clicked |
| Operation | a select: Unloading, Disposal, Restack, Loading | Unloading |
| Unit | a select: Standard (48×40), XL | Standard |
| Q-ty | a number input, 1–10 000 | empty |
| Trailer | a text input, enabled for Unloading and Loading only; `null` is sent when it is disabled | the trailer of the selected shipment's arrival, when it has arrived; set again when the Sub-order changes |
| Billable | a checkbox; set again whenever the Operation changes | checked for Unloading and Loading |
| Photos | a number input, 0–99 | 0 |

- "Cancel" closes the dialog without a request. "Record" posts with the `version` the page loaded
  and is disabled while the request is in flight.
- On `201` the dialog closes and the page refetches the order and `/me` — the sidebar badge
  changes when an Alert opens or closes.
- On an error the dialog stays open and shows the `detail` of the `ProblemDetails`; the field
  errors of a `400` are shown next to their fields, matched by `errors[].name`. After a `409`
  `order.version_conflict` the page refetches the order behind the dialog, so "Record" can be
  pressed again with the new version.
- On a `Draft` and on a `Completed` order the buttons are disabled with the title "The order is
  not active".
- All text is written with `textContent`.

## Acceptance criteria

- [ ] On the seeded database: a Loading of 6 Standard on FR001383 is `409` naming the 5 On stock
      and writes nothing; a Loading of 5 is `201` and leaves 0 On stock.
- [ ] An Unloading of 2 Standard on FR001674-2 with a photo is `201` and resolves its Quantity
      delta alert; the Missing photo alert stays open.
- [ ] An Unloading on FR001681 starts it (`InProgress`); without a photo it raises a Missing photo
      alert, above the declared quantity a Quantity delta alert.
- [ ] Repeating any successful request unchanged is `409` `order.version_conflict`.
- [ ] With `Microsoft.EntityFrameworkCore.Database.Command` at `Information`, one request writes
      with a single command batch that ends in `UPDATE orders … WHERE id = @… AND version = @…`.
- [ ] Scalar shows the endpoint with request and response schemas.
- [ ] Build with 0 warnings, `dotnet format --verify-no-changes` clean, `dotnet test` green.
- [ ] *Browser session:* the form on the detail page records an Operation and shows a refusal
      without a reload.

The seeded checks change the Compose database; `docker compose down -v` afterwards restores the
seed on the next start.

## Tests

Integration, through HTTP, in `Freitty.Api.IntegrationTests/Orders/`. Each test builds its own
orders under its own company and never writes to the shared seeded database of step 3. The
`FakeTimeProvider` stands at or after the last instant used to build an order.

- Each validation rule, with the name of the field in the error.
- Each row of the ordered checks: its status, its `code` and that nothing was written.
- The first Operation starts a `Ready` order.
- An Unloading above the declared quantity opens a Quantity delta alert; the missing quantity on a
  received short shipment resolves it; an Unloading without a photo opens a Missing photo alert.
- A refused Operation leaves the order unchanged: no Operation row, the same version.
- A repeated request is `409` and adds nothing.
- An order of another company → `404`.
- **The two lost races, made deterministic.** The test project has a `SaveChangesInterceptor`,
  added in `ConfigureTestServices` with
  `ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(…))`. It does nothing until
  a test arms it with an action, after the orders are built. In `SavingChangesAsync` it takes the
  action with `Interlocked.Exchange` — disarming itself before the competing save, which passes
  through the same interceptor — awaits it, then lets the request's save continue. The action
  loads the same order in a scope of its own, records an Operation and saves.
  - *On the order row:* both writers record an Operation that raises no Alert — an Unloading with
    a photo, within the declared quantity. The request answers `409` `order.version_conflict` with
    the "while this request was being processed" detail; the order's version is one above the
    start and it has only the competing Operation.
  - *On the Alert index:* a `Ready` order; both record an Unloading with `photoCount` 0. EF Core
    sends the Alert insert before the update of the order row, so the index answers first. The
    request answers `409`; the order has one Operation and exactly one open Missing photo alert.
- A smoke test with two simultaneous requests: one `201`, the other `409`, one new Operation.

## Out of scope

- Editing or deleting an Operation; reversal rows —
  [ADR 0005](../adr/0005-stored-state-not-event-sourcing.md).
- Finishing receiving, resolving an Alert, completing an order, supplies and dock assignment
  through the API.
- An idempotency key stored on the Operation — the version check covers a repeat.
- Authentication: every request acts within the company of the current user of step 4.
