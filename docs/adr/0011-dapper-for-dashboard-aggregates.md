# 0011 — Dapper and hand-written SQL for the dashboard aggregates

- Status: accepted
- Date: 2026-11-18

## Context

The dashboard is a handful of aggregates over time: counts in two rolling windows, a count as of
the start of the week, a series of calendar buckets cut in a time zone with the empty buckets
present, sums of invoice amounts beside counts of orders. Everything else in the application reads
through EF Core projections.

## Decision

The dashboard aggregates are SQL written by hand and executed with Dapper, behind one interface in
the application layer (`IDashboardReader`). EF Core stays for everything that is a shape of rows:
the order cards on the same screen, the list, the detail and all writes.

Enum values, the current instant and the time zone are parameters. No statement holds a literal
status or calls `now()`.

## Alternatives

- **EF Core LINQ** — one data access technology. Rejected: a truncation in a time zone joined to
  a generated series of buckets does not translate, so the chart would be grouped in memory or
  assembled from a query per bucket.
- **EF Core raw SQL (`SqlQuery<T>`)** — the same SQL without a second library. Viable. Not taken:
  it composes with LINQ, which invites wrapping the statement in a subquery; Dapper states plainly
  that this is SQL and nothing else.
- **A database view or materialized view** — the SQL lives where it runs. Rejected: the windows
  depend on the current instant and the zone, so the view would need parameters it cannot take,
  and a materialized view needs a refresh for seventy orders.

## Consequences

- The SQL the dashboard runs is the SQL in the repository, readable and reviewable as SQL, and it
  can be pasted into `psql` with its parameters for an `EXPLAIN`.
- Two ways to read the database. The rule that separates them: aggregates over time are Dapper,
  rows are EF Core.
- The statements are not checked by the compiler and do not follow a rename. They name columns in
  `snake_case` as the naming convention of step 2 produces them, and integration tests on real
  PostgreSQL are what catches a drift.
- The numbers that must agree come from one statement each, so a tile and the chart summary cannot
  contradict each other.
- The reader opens its own connection, so one dashboard request uses two — the context's for the
  cards and the reader's for the aggregates — and the two do not share a snapshot. A card and a
  tile can differ by a write that lands between them; the next request agrees again.
- Dependency injection now owns an `NpgsqlDataSource`, which the context uses as well.
- If the plan is cut for time, the dashboard falls back to EF Core queries per figure; the
  interface stays and only its implementation changes.
