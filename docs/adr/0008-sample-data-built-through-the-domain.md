# 0008 — Sample data is built through the domain at startup, on relative dates

- Status: accepted
- Date: 2026-11-17

## Context

The application is judged by its deployed instance, so that instance needs data: orders in every
status, alerts, invoices, three months of history for the chart. The data has to agree with the
domain rules of [ADR 0003](0003-order-is-one-aggregate.md) and
[ADR 0004](0004-quantities-have-one-source.md) — the mockup it starts from does not agree with
itself. And most screens are about time: "completed in the last 30 days", "today", a weekly chart.
Data with fixed dates is correct on one day only.

## Decision

The API seeds an empty database when it starts, right after the migrations
([ADR 0007](0007-migrations-applied-at-startup.md)), when `Seed:Enabled` is set. The setting is off
by default.

Every order is built by calling the `Order` aggregate with the instants at which things happened —
the same methods a handler calls. A refused call stops the startup.

Dates are relative: the seed takes the day before it runs, in the time zone of the hubs, as its
anchor and places every event a fixed number of days from it, at a fixed local time.

The application never deletes or refreshes sample data. Starting over means an empty database.

## Alternatives

- **`HasData` in the EF Core model** — the built-in way. Rejected: it inserts rows, so nothing
  checks them against the aggregate; it needs literal keys and literal dates, and every change of
  the sample data becomes a migration.
- **A SQL script** — fast and independent of the code. Rejected for the same reason, and it has to
  be kept in step with the schema by hand. It is the right tool for the bulk data of the query-plan
  measurements, where volume matters and rules do not.
- **A separate seeding command or container** — keeps demo data out of the API's startup. Rejected
  for now, like the migration bundle of ADR 0007: a second thing to build, run and deploy for one
  instance.
- **Fixed dates from the mockup** — nothing to compute. Rejected: outside April 2026 the dashboard
  shows zero completed orders and an empty chart.
- **The seeding day itself as the anchor** — the mockup's "today" stays today. Rejected: an API
  started in the morning would hold operations dated later that day, and the aggregate refuses a
  change earlier than the last one, so the first write to such an order would fail.
- **Refreshing stale data automatically** — the demo never ages. Rejected: it puts a code path that
  deletes every table into the API, triggered by a date or a setting.

## Consequences

- Sample data cannot violate a domain rule, and the seed is a second consumer of the whole
  lifecycle: a rule that makes realistic data impossible to build shows up here first.
- One command still gives a working, populated stack — locally and in CI, where the `stack` job now
  exercises the seed on every run.
- The data ages. A database seeded a week ago shows a week-old picture; before a demo the database
  is reset. Accepted.
- Nothing seeded is dated on or after the seeding day except a Scheduled date. The mockup's
  "today" appears as yesterday.
- Demo data lives in the production assembly. The switch is off unless the environment turns it on,
  and the seed refuses a database that already has a company.
- Every order is built in memory, tracked and inserted row by row. At seventy orders that is not
  noticeable; it is the reason the volume data set is not built this way.
