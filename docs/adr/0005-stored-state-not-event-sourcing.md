# 0005 — Stored state with an append-only ledger, not event sourcing

- Status: accepted
- Date: 2026-11-17

## Context

An order produces a stream of intermediate facts: operations, alerts raised and resolved, status
changes. Questions about the past are plausible — what was on stock at ten o'clock, when was the
client told about a shortage — and so are corrections. That is the usual argument for event
sourcing, so the choice has to be explicit.

The system is read-dominated: three screens and one write flow, with filtering, search and
pagination required to run in the database.

## Decision

Entities are stored as current state in relational tables. History is kept where the business has
history, in the shape the business already uses:

- Operations are a ledger the domain only appends to: it never updates or deletes one. Quantities
  at any moment are sums over Operations with `applied_at` up to that moment.
- An Alert keeps `raised_at` and `resolved_at`.
- An Order keeps the instants of its milestones — created, submitted, started, completed — which
  give its status at any moment.
- A correction, when it is built, is a new row: a reversal that points at the Operation it reverses
  and takes its quantity back. Nothing is edited or removed.

## Alternatives

- **Event sourcing for the Order aggregate** — full history and replay for free. Rejected: every
  screen would read a projection, so the relational schema would exist anyway, as a derived
  artifact with its own rebuild and consistency machinery; and event versioning, snapshots and
  idempotent projections are a permanent cost for one write flow.
- **Stored state with a generic audit table** (who changed which field, when) — answers questions
  the ledger does not. Not rejected, postponed: nothing in the task needs it, and it can be added
  beside the model without changing it.

## Consequences

- Queries, indexes and query plans work on the real model, not on projections of it.
- "What was the state at T" is answerable for quantities, alerts and status, and not for anything
  else. For status it holds because no transition goes back: the day an order can be reopened or
  put on hold, a milestone column is no longer enough and a table of status changes replaces it. A field that is overwritten — the carrier, the schedule, the dock — keeps no history, and
  nothing records who acted.
- The ledger runs on recording time: `applied_at` is when an Operation was entered, so "on stock at
  ten" means "as recorded by ten". A separate instant for when the work was physically done is one
  more column.
- Corrections are not built. Until they are, a mistyped or repeated Unloading cannot be undone:
  nothing subtracts from Actual quantity, so it leaves a delta that can only be accepted by hand.
  The write flow therefore has to refuse a repeated request, and the reversal row above is the
  first thing to add before real use. It is an additive migration.
- Append-only is a rule of the domain, backed by foreign keys that refuse to delete an order with a
  ledger. It is not enforced against direct SQL.
- The history a client sees — the timeline of an order — can be assembled from what is stored:
  milestones, Operations and Alerts.
- The road to more history is incremental — an audit table, or domain events with an outbox when
  other systems must react. Full event sourcing becomes worth its cost if a complete audit trail
  turns into a legal requirement or several systems subscribe to order events.
