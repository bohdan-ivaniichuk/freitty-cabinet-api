# 06 — Dashboard

## Goal

`GET /api/v1/dashboard` returns everything the Dashboard screen shows: the KPI tiles, the first
active orders and the activity chart with its period switch. The aggregates are hand-written SQL
through Dapper — `date_trunc`, `FILTER (WHERE …)`, CTEs and `generate_series`. The number on the
sidebar badge comes from the same SQL.

## Decisions

- **Dapper for the aggregates, EF Core for the cards** —
  [ADR 0011](../adr/0011-dapper-for-dashboard-aggregates.md). The SQL lives in Infrastructure
  behind one interface in Application (`IDashboardReader`). The "Active Orders" cards reuse the
  card reader of [step 4](04-order-list.md): one definition of a card.
- **Every number is computed, and numbers that must agree come from one statement.** "Completed
  (30 d)" and its comparison are two `FILTER`s of one query; the best bucket is the maximum of the
  series the chart draws. The contradictions of the mockup cannot be reproduced.
- **Need attention = awaiting your action + alerts.** Awaiting your action is what only the client
  can move: drafts it has not submitted and orders with an unpaid invoice. Alerts is the number of
  orders with an open Alert — the Alerts tab of the list. The total is their sum. In the seed the
  two do not overlap; an order that is in both — invoiced while an Alert is open, which the domain
  allows — needs two things and is counted in both.
- **Spend is what was invoiced**, counted at `issued_at`. Payment is a later fact and would move
  the chart whenever a client pays late.
- **"30 days" is rolling**: `[now − 30 d, now)` and `[now − 60 d, now − 30 d)`. Buckets are
  calendar units.
- **Buckets are cut in the reporting time zone** of step 4
  ([ADR 0010](../adr/0010-one-reporting-time-zone.md)) with the three-argument
  `date_trunc(unit, instant, zone)` — a week that starts on Monday in Toronto starts at 04:00 or
  05:00 UTC. Weeks start on Monday.
- **The clock is injected.** `now` comes from `TimeProvider` and goes to SQL as a parameter; no
  statement calls `now()`.
- **Enum values go to SQL as parameters** taken from the enums — never a literal `status = 4`.
- **Empty buckets are part of the series.** `generate_series` produces the axis, the data is
  joined to it; the page never has to invent a missing week.
- **The axis is stepped in the reporting zone too.** `timestamptz ± interval` and the
  three-argument `generate_series` step in the session zone, which is UTC: across a daylight
  saving change the generated starts stop matching what `date_trunc` returns, and most buckets
  come back empty. The form that holds, checked on PostgreSQL 18:
  `generate_series(date_subtract(date_trunc(@unit, @now, @zone), @back, @zone), date_trunc(@unit, @now, @zone), @step, @zone)`,
  with `start = (bucket AT TIME ZONE @zone)::date`.
- **Rounding happens in SQL** (`round`, half away from zero), so the same number comes out of
  `psql` and out of the API.
- **The period switch works**: one statement, a different unit and length.

## Contract

### `GET /api/v1/dashboard?period=Week`

`period` is `Day`, `Week`, `Month` or `Quarter`; `Week` when absent. The example shows the shape;
its numbers are illustrative.

```jsonc
{
  "activeOrders": { "count": 7, "changeThisWeek": 2 },
  "completed": { "last30Days": 24, "previous30Days": 20 },
  "needAttention": {
    "total": 4,
    "awaitingAction": 2,
    "alerts": 2,
    "alertOrders": [{ "number": "FR001383", "alertTypes": ["QuantityDelta"] }]
  },
  "orders": [ /* up to four cards of step 4 */ ],
  "activity": {
    "period": "Week",
    "buckets": [{ "start": "2026-02-02", "completedOrders": 3, "spend": 1234.00 }],
    "summary": {
      "completedLast30Days": 24,
      "completedChangePercent": 20,
      "spendLast30Days": 9876.00,
      "averageSpendPerOrder": 411.50,
      "best": { "start": "2026-03-16", "completedOrders": 7, "spend": 2890.00 }
    }
  }
}
```

| Field | Definition |
|---|---|
| `activeOrders.count` | orders in `Ready` or `InProgress` |
| `activeOrders.changeThisWeek` | that count minus the orders that were active at the start of this week — `submitted_at < start AND (completed_at IS NULL OR completed_at >= start)`, with `start = date_trunc('week', @now, @zone)` |
| `completed.last30Days`, `previous30Days` | orders by `completed_at` in the two rolling windows |
| `needAttention.awaitingAction` | drafts + orders with at least one invoice where `paid_at IS NULL` |
| `needAttention.alerts` | orders with an open Alert — equals `alerts` of `/orders/counts` without filters |
| `needAttention.total` | the sum of the two |
| `needAttention.alertOrders` | up to three of those orders, by the `raised_at` of their latest open Alert, newest first, then by number; each with its distinct open Alert types in the order of the enum's values — the rule of the card's `openAlertTypes` |
| `orders` | the first four orders in `Ready` or `InProgress` in the list's default order |
| `activity.buckets` | `Day`: 14 buckets of `1 day`; `Week`: 12 of `1 week`; `Month`: 6 of `1 month`; `Quarter`: 4 of `3 months` (unit `quarter`) — each series ending with the current bucket, oldest first. `start` is the local date the bucket begins. `completedOrders` by `completed_at`, `spend` = sum of invoice amounts by `issued_at` |
| `summary.completedLast30Days` | the same number as `completed.last30Days`, from the same statement |
| `summary.completedChangePercent` | `(last − previous) / previous × 100`, rounded to a whole number; `null` when `previous` is zero |
| `summary.spendLast30Days` | invoice amounts with `issued_at` in the last 30 days |
| `summary.averageSpendPerOrder` | that spend divided by the number of orders invoiced in the window, two decimals; `null` when none |
| `summary.best` | the bucket of the series with the highest spend, the later one on a tie; `null` when every bucket has zero spend |

Everything is scoped to the company of the current user.

### `GET /api/v1/me`

Gains a number — `"needAttention": 4` — equal to `needAttention.total` of the dashboard and read
by the same statement. The shell shows it on the "Orders" badge.

### Errors

An unknown `period` → `400` `ProblemDetails` with the field error.

### Code

```
src/Freitty.Application/Dashboard/        the query, its handler and validator, the DTOs,
                                          IDashboardReader
src/Freitty.Infrastructure/Dashboard/     DashboardReader (Dapper) and its SQL
src/Freitty.Api/Endpoints/Dashboard/      the endpoint
```

- Four statements at most per dashboard request for the aggregates: the KPIs, the series, the
  attention counts, the alert orders. `/me` runs only the attention counts. The SQL is raw string
  constants next to the reader, formatted to be read.
- Result columns are aliased to the property names of the row types they are read into; no global
  Dapper setting is changed.
- One `NpgsqlDataSource` is registered in DI (`AddNpgsqlDataSource` from
  `Npgsql.DependencyInjection`) and `AppDbContext` is configured to use it; `UseNpgsql` with a
  connection string registers none. Dapper opens a connection from it per request and disposes
  it; it does not share the context's connection.
- `Directory.Packages.props` gains `Dapper` (2.1.86 or later — earlier versions do not read
  `date` into `DateOnly`) and `Npgsql.DependencyInjection` at the version of the Npgsql provider.
- Dapper specifics, checked on 2026-11-18 and to be confirmed: every instant parameter is passed
  in UTC; `timestamptz` maps to a `DateTimeOffset` property with a setter but not to a positional
  record or a scalar without a type handler; an array of enum values is sent as `int[]` with
  `= ANY(@values)`, since `IN @list` and enum arrays fail on Npgsql.

## Acceptance criteria

- [ ] On the seeded database, on the day it was seeded: active orders 7, completed 24 and 20,
      Need attention 4 (2 awaiting, 2 alerts, FR001383 listed before FR001674), four cards
      (FR001681, FR001679, FR001678, FR001383), and twelve weekly buckets, the last starting on
      the Monday of the current week and each of the eleven before it with `completedOrders > 0`.
- [ ] `needAttention.alerts` equals `alerts` of `GET /api/v1/orders/counts`.
- [ ] The sum of `spend` over the `Quarter` buckets equals the sum of all seeded invoice amounts.
- [ ] Each period returns its number of buckets, oldest first, the last containing today.
- [ ] No SQL statement contains an enum literal or `now()`.
- [ ] Scalar shows the endpoint; an unknown period is a `400`.
- [ ] Build with 0 warnings, `dotnet format --verify-no-changes` clean, `dotnet test` green.

## Tests

Integration, through HTTP, with `FakeTimeProvider` and orders built through the aggregate under a
company of the test's own:

- The tiles against a known set: the boundaries of the 30-day windows, an order completed exactly
  at a boundary, the change since the start of the week.
- Need attention: a draft, an order with an unpaid invoice, an order with a paid one; Alerts on
  several orders; one order with two open Alerts counted once and listed with both types.
- The series for each period: the number of buckets, their start dates, empty buckets present.
- A weekly series that spans a daylight saving change: every bucket starts on a Monday in local
  time on both sides of it, and an order completed on each side is counted in its own bucket.
- A bucket boundary in the reporting zone: an order completed at 23:30 local time on Sunday
  belongs to that week, one completed 00:30 on Monday to the next — the two instants fall on the
  same UTC day.
- Spend from invoices: amounts per bucket, the 30-day total, the average, an order without an
  invoice, the best bucket and its tie rule, `null` when there is no spend.
- `completedChangePercent` is `null` when the previous window is empty.
- Orders of another company change nothing.
- An unknown period → `400`.
- `/me` returns a `needAttention` equal to the dashboard's `needAttention.total`.
- An order with an open Alert and an unpaid invoice is counted in both figures.

## Out of scope

- The page itself — [step 7](07-frontend.md). Top-up, billing, notifications.
- A reporting zone per company or per user — ADR 0010.
- Caching the aggregates.
