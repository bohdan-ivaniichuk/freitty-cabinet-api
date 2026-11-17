# 08 — Write flow

> **Draft, not groomed.** Written ahead of the grooming session so it does not start from zero.
> Everything under *Decisions* is a proposal; *Open questions* is what the session has to settle.
> Nothing here is input for implementation yet.

## Goal

One write through the API: recording an Operation on a shipment. It is not required by the task; it
is there to show a transaction, optimistic concurrency and the domain rules at work — the ledger
grows, the order starts, a Quantity delta alert opens or resolves, all or nothing.

## Decisions

- **The handler orchestrates, the aggregate decides** (rule 1): load the order whole, call
  `RecordOperation`, save. No rule is repeated in the handler or the validator.
- **One `SaveChanges`, one transaction.** The Operation, the alert it raises or resolves, the
  status change and the order row are written together.
- **Optimistic concurrency on the order row.** Two requests that change the same order cannot both
  win: the second gets `409`. Without it two concurrent Unloadings each see the old quantities, one
  resolves the alert and the other leaves it — the alert ends in a state neither computed.
- **The token must change with every change of the aggregate.** EF Core writes the order row only
  when one of its values changed, so a timestamp is not enough: two changes with the same instant
  leave the row untouched. Step 2 therefore gave `Order` an integer `Version` that every successful
  method increments. Proposal: map it as the concurrency token. See open question 1 for `xmin`.
- **A lost race has two faces.** `DbUpdateConcurrencyException` on the order row, and a unique
  violation on the open-alert index when two writers raise the same alert. Both become `409`. The
  violation is recognised by the name of the constraint, not by the error class alone.
- **No retry inside the request.** A `409` goes back to the caller, who reloads and decides. A retry
  would need a fresh context and a fresh aggregate; reusing the failed one repeats the write.
- **A domain refusal is not an exception.** The `Result` of the aggregate becomes a
  `ProblemDetails`; the HTTP status follows `DomainError.Kind` (`NotFound`, `Conflict`), not a list
  of code strings.
- **A repeated request must not append twice.** An Unloading cannot be taken back: nothing
  subtracts from Actual quantity. A double click or a client retry that records the same Unloading
  twice leaves a permanent false delta. See open question 2.

## Contract

### `POST /api/v1/orders/{number}/operations`

Request: the shipment (by its position in the order), type, Unit Type, quantity, trailer number
(for Unloading and Loading), billable, photo count.

Response `201`: the recorded operation, and what the page must refresh — the order's status, the
shipment's quantities and On stock, its open alerts.

| Outcome | Status |
|---|---|
| Recorded | `201` |
| Invalid payload (missing field, quantity ≤ 0, unknown enum value, trailer number where it does not belong) | `400` |
| Unknown order or shipment | `404` |
| The order's state refuses it (not active, more than On stock) | `409` or `422` — open question 3 |
| Another request changed the order first | `409` |

### Schema

`orders.version` exists since step 2. Making it the token changes the model, not the table. Mapping
`xmin` instead adds a migration file that contains no DDL — without the file `Migrate()` refuses to
start because the model has changes no migration covers.

## Acceptance criteria

- [ ] Recording an Unloading above declared opens a Quantity delta alert in the same response; the
      list's Alerts tab and the dashboard show it at once.
- [ ] Recording the missing quantity on a received shipment resolves its alert.
- [ ] A Loading beyond On stock is refused with a message that names the quantities, and nothing is
      written.
- [ ] Two simultaneous requests on one order: exactly one succeeds, the other gets `409`, and the
      alerts match the ledger afterwards.
- [ ] The log shows one transaction per request.

## Tests

- Each outcome of the table above through HTTP — integration
- The first operation starts a `Ready` order — integration
- A refused operation leaves the database unchanged — integration
- Two concurrent writers on one order: one `409`, a consistent result — integration
- Two writers raising the same alert: one `409`, one open alert — integration
- The repeated-request rule chosen in open question 2 — integration

## Out of scope

- Editing or deleting an operation; reversal rows.
- Supplies, dock assignment, completing an order through the API.
- Authentication: the actor is the seeded user.

## Open questions

1. **`Version` or `xmin`.** The plan names `xmin` — a PostgreSQL system column and a good thing to
   explain. It changes only when the row is written; the counter of step 2 guarantees that write.
   So both work: the counter itself as the token (plain, visible in tests, portable), or `xmin` as
   the token with the counter only forcing the write (shows a PostgreSQL feature). Which one is
   presented?
2. **Idempotency.** Cheapest: the request carries the version the page loaded, so a repeat is a
   `409`. More robust: a key supplied by the caller, stored on the operation and unique per
   shipment, so a repeat returns the first result. Which, and is the second worth a column?
3. **`409` or `422` for a refusal by the domain.** One reading: `409` when the order is in the wrong
   state, `422` when the request can never be valid as sent. Is the distinction worth making for
   the caller?
4. **More than one write?** Finishing receiving and resolving an alert exist in the domain. Without
   them the demo can open an alert through the API but can close one only by recording more
   freight. Add `POST …/shipments/{position}/receiving` and `POST …/alerts/{id}/resolution`?
5. **Isolation level.** Read committed plus the token is the proposal. The presentation should be
   able to say why not `SERIALIZABLE` and why not `SELECT … FOR UPDATE` — agree on the answer.
6. **How the shipment is addressed**: by position (`1`), which is what the page shows, or by id?
7. **The form on the detail page.** Part of this step, or of step 7?
8. **How the aggregate is loaded for the write**: one query with every collection, or split
   queries? With split queries the root is read first, so the token still protects the write —
   confirm on the real query.
