# 06 — Dashboard

> **Draft, not groomed.** Written ahead of the grooming session so it does not start from zero.
> Everything under *Decisions* is a proposal; *Open questions* is what the session has to settle.
> Nothing here is input for implementation yet.

## Goal

The Dashboard screen gets its numbers from the API: the KPI tiles, the active orders and the
activity chart with its period switch. The aggregates are hand-written SQL through Dapper — the
place where the plan shows `date_trunc`, `FILTER (WHERE …)` and CTEs.

## Decisions

- **Dapper for the aggregates, EF Core for the cards.** The KPI and chart queries are SQL files or
  constants in Infrastructure, behind one interface in Application. The "Active Orders" cards reuse
  the card projection of step 4 — one definition of a card.
- **Every number is computed, and numbers that must agree come from the same query.** "Completed
  (30 d)" and "+18 % vs previous month" are two windows of one statement; "Best week" is the
  maximum of the series the chart draws. The contradictions listed in `mockup-analysis.md` cannot
  be reproduced.
- **Enum values go to SQL as parameters** taken from the enum — never a literal `status = 4`.
- **The clock is injected.** "Last 30 days", "this week" and the chart buckets are computed from
  `TimeProvider`; tests fix it.
- **Buckets are cut in local time**, with `date_trunc(unit, instant, zone)`, not in UTC: a week
  that starts on Monday in Toronto starts at 04:00 or 05:00 UTC.
- The period switch works (`mockup-analysis.md`, open question 2): the same query with a different
  bucket.

## Contract

### `GET /api/v1/dashboard?period=Day|Week|Month|Quarter`

- **Active orders** — count of `Ready` and `InProgress`; change since the start of the week.
- **Completed (30 d)** — count by `completed_at`; the same count for the previous 30 days.
- **Need attention** — total; "awaiting your action" count; alerts: count, and for the tile's
  sub-line the order number and alert type.
- **Active orders list** — the first N cards (step 4's card, with destination).
- **Activity** — a series of buckets, each with completed orders and spend; and the summary lines:
  completed and spend for the last 30 days, change against the previous 30, average spend per
  order, the best bucket.

### Errors

An unknown `period` → `400` `ProblemDetails`.

## Acceptance criteria

- [ ] Every figure on the dashboard changes when the data changes; none is a constant.
- [ ] The count of completed orders in the tile equals the sum of the chart's buckets over the same
      window, and the percentage follows from the two counts.
- [ ] The Alerts figure equals the counter of the Alerts tab of step 4.
- [ ] Each period returns buckets cut on local boundaries.
- [ ] The SQL contains no enum literal.

## Tests

- KPIs against a known set of orders with the clock fixed: boundaries of "30 days" and of the week
  — integration
- The chart per period: bucket boundaries in the hub's zone, empty buckets present, the best bucket
  — integration
- Spend from invoices: amounts, the average, an order without an invoice — integration
- Need attention: alerts on several orders, one order with two alerts counted once — integration
- An unknown period → `400` — integration

## Out of scope

- The page itself — step 7. Top-up, billing, notifications.

## Open questions

1. **"Awaiting your action".** Two of the three in the largest tile, and undefined in the mockup.
   Candidates that the model already stores: drafts the client has not finished, and invoices
   issued and not paid. Both, one of them, something else?
2. **The badge on "Orders" in the sidebar** (3). The same number as Need attention?
3. **Spend.** Counted when an invoice is issued or when it is paid? "Average per order": over
   completed orders or over invoiced ones?
4. **The periods.** What does each button show: `Day` — the last 14 days by day; `CW` — the last 12
   calendar weeks; `Month` — 12 months; `Quarter` — 8 quarters? The mockup draws about eleven bars
   under "Month".
5. **Whose time zone.** A company's orders can sit in hubs of different zones. One zone for the
   dashboard — the company's (a new column), or a single platform setting? Both seeded hubs are in
   Toronto, so the question is only how to state it.
6. **"▲ 2 this week".** Active now minus active at the start of the week, computed from the
   milestone instants?
7. **Which orders are "Active Orders" on the dashboard and how many.** The mockup shows a completed
   one among them; `mockup-analysis.md` says active means not completed and not a draft. Sorted how?
8. **Dapper and `timestamptz`.** Whether Dapper needs a type handler for `DateTimeOffset` with
   Npgsql is to be checked, not assumed; and whether Dapper shares the connection of `AppDbContext`
   or takes its own from an `NpgsqlDataSource`.
9. **If time is short** the plan allows the dashboard in EF Core without Dapper. Decide at grooming
   whether that cut is taken.
