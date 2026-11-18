# 0013 — Optimistic concurrency on `Order.Version`

- Status: accepted
- Date: 2026-11-18

## Context

Recording an Operation reads the whole `Order` aggregate, lets it decide — is there enough On
stock, does a Quantity delta alert open or resolve — and writes the result. Two requests that read
the same state both decide on it: two Loadings of the last five pallets both pass, two Unloadings
leave an Alert in a state neither computed. And an Unloading cannot be taken back, so a request
that is sent twice must not be recorded twice.

Every successful method of the aggregate already increments `Order.Version`
([step 2](../steps/02-domain.md)), so the order row is written by every change.

## Decision

`Order.Version` is the EF Core concurrency token: the `UPDATE` of the order row carries
`WHERE version = <the version that was read>`, and a write that finds another version fails with
`409`.

The request carries the version the caller saw (`expectedVersion`). The handler compares it before
calling the aggregate, so a stale page, a double click and a retry are refused as well.

The transaction stays at `READ COMMITTED`, and nothing retries inside the request.

## Alternatives

- **`xmin` as the token** — the PostgreSQL system column changes on every write of the row, with
  no column of our own. Rejected: it is an opaque transaction id to hand to an API client, it ties
  the model to one database, and it needs a migration that contains no DDL. The counter does the
  same job and is readable in a test, in the log and in the response.
- **`SELECT … FOR UPDATE` on the order row** — writers queue instead of failing. Rejected: it holds
  a lock across application code, it does nothing for the stale page or the repeated request, and
  those need a version anyway.
- **`SERIALIZABLE`** — the database detects the conflict itself. Rejected: every write then needs a
  retry loop for serialization failures, and it still does not stop a request that is simply sent
  twice.
- **An idempotency key stored on the Operation** — a repeat returns the first result instead of a
  `409`. The more forgiving contract, and the one to add when clients retry automatically. Not
  taken now: it costs a column and a unique index, and the form reloads after a write.
- **A timestamp as the token** — `UpdatedAt` exists. Rejected: two changes can carry the same
  instant, and EF Core writes a row only when a value changed.

## Consequences

- Two writers on one order: exactly one wins, the other gets `409` and reloads. No lost update, no
  retry loop, no lock held.
- The token is part of the API: the detail response returns `version`, the write sends it back.
- The aggregate is loaded with split queries, which are not one snapshot. That is safe because the
  order row is read first: a change that lands between the statements has already moved the
  version, and the save fails.
- A second way to lose a race remains: two writers that both insert the same open Alert meet the
  partial unique index before the order row is updated. That violation is mapped to `409` too.
- A busy order makes clients reload. At the rate a hub records Operations on one order this is not
  a concern; a queue per order would be the answer if it became one.
- The rule that every method of the aggregate increments `Version` is now load-bearing: a change
  that forgot to would not be protected.
- The aggregate also requires each change to be no earlier than the last one, and the handler
  reads the clock after it has loaded the order. With one instance and a clock that does not step
  back that always holds. A clock that jumps backwards — or two instances whose clocks differ by
  more than the gap between two writes — makes the aggregate reject the instant as a broken
  contract, which is a `500`, not a `409`. Accepted for one instance; several instances need a
  shared time source or a rule that takes the later of the two instants.
