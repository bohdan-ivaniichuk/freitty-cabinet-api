# 04 — Order List

> **Draft, not groomed.** Written ahead of the grooming session so it does not start from zero.
> Everything under *Decisions* is a proposal; *Open questions* is what the session has to settle.
> Nothing here is input for implementation yet.

## Goal

The Order List screen gets its data from the API: cards with filters, search and pagination, all
executed in PostgreSQL, and the counters of the tabs. This is the first endpoint, so the API
framework, the error contract, OpenAPI and the handler pipeline are wired here, and the first
performance indexes are added with their query plans.

## Decisions

**Wired in this step** (deferred from steps 1 and 2)

- FastEndpoints under `/api/v1`; `ProblemDetails` for every error; OpenAPI with Scalar.
- The handler pipeline: logging → FluentValidation → handler, on the FastEndpoints command bus.
- `IAppDbContext` in Application, implemented by `AppDbContext`.
- One JSON converter that writes enums as strings (rule 11).
- The current company and user. There is no authentication, so they come from one small
  abstraction that returns the seeded company and "User 1". Every query is scoped by company from
  the first day; authentication later replaces the implementation, not the queries.
- `Microsoft.EntityFrameworkCore.Database.Command` back to `Information` in
  `appsettings.Development.json`, so `dotnet run` shows the SQL (promised in step 1).

**The query**

- One read handler projects straight to the card DTO: `AsNoTracking`, `Select`, no entity loaded.
  The computed members of the aggregate are not used — they return zeros in a projection
  ([step 2](02-domain.md), *Computed, never stored*).
- Quantities are sums in SQL over `cargo_lines` and `operations` for the orders of the page.
- Search matches the order number and the Ref N of any shipment, case-insensitively, as a `UNION`
  of order ids — measured in the step 2 review as fast with trigram indexes, where
  `number ILIKE … OR EXISTS (…)` scans the table. A sub-order label typed in full
  (`FR001676-2`) is matched by its order number.
- Pagination is offset / limit with a total count. Every sort ends with `id`, so pages never repeat
  or skip a row when two orders share a date. Keyset pagination is the scale-up path and is named
  in the presentation.
- The tab counters are their own endpoint. The Alerts counter is a separate statement driven by
  the partial index on open alerts; the others are one `GROUP BY`.
- Dates are filtered and shown in the hub's time zone (`hubs.time_zone`).

**Indexes**

- `pg_trgm` GIN on `orders.number` and `shipments.ref_number`; a composite index for the list's
  filter and sort. Each comes with `EXPLAIN ANALYZE` before and after in `docs/explain/`, on a
  data set large enough for the planner to care.

## Contract

### `GET /api/v1/orders`

| Parameter | Values | Meaning |
|---|---|---|
| `type` | `CrossDock`, `Consolidation` | The Cross-Dock and Consolidation tabs |
| `status` | `Draft`, `Ready`, `InProgress`, `Completed` | The Status filter; `Draft` is the Drafts tab |
| `alerts` | `true` | The Alerts tab: orders with an open Alert |
| `hub` | hub id | The Hub filter |
| `from`, `to` | dates | The Date filter, on the Scheduled date |
| `q` | text | Search |
| `page`, `pageSize` | integers | Pagination; `pageSize` has an upper bound |

Response: `items`, `page`, `pageSize`, `total`. A card carries what the mockup's card shows:

- number, type, Cross-Dock kind, status, whether an Alert is open and of which type;
- who created it: name, initials, role;
- hub (name, short name, time zone), transfer hub (short name), Scheduled date;
- declared quantity per Unit Type; declared and actual totals when they differ;
- shipments: sub-order label, Ref N, pallets, open Alert;
- transport: kind, carrier name or driver name; destination (dashboard card);
- number of outbound trailers (Consolidation);
- the "Next" line, as a code plus its data — the frontend owns the wording.

### `GET /api/v1/orders/counts`

`all`, `crossDock`, `consolidation`, `alerts`, `drafts`.

### Errors

Invalid parameters → `400` `ProblemDetails` with the field errors.

### Also likely needed here

`GET /api/v1/me` — the shell of every page shows the user's name and initials, the company balance
and the badge on "Orders". It is not in the plan's API list.

## Acceptance criteria

- [ ] Each tab, filter and the search return the orders they should, and the SQL log shows the work
      done in the database: no filtering or paging in memory.
- [ ] The counters agree with the list for the same filters.
- [ ] An invalid `pageSize`, date range or enum value is a `400` `ProblemDetails`.
- [ ] Scalar shows both endpoints with their schemas; enums are strings.
- [ ] `docs/explain/` holds the plans before and after each index, and the index is used.

## Tests

- Each tab and each filter, alone and combined — integration
- Search by order number, by Ref N, by a sub-order label, case-insensitively — integration
- Pagination: page boundaries, the total, a stable order across pages — integration
- The quantities, the Alert flag and the trailer count of a card with a known ledger — integration
- Counters against the same data — integration
- Validation failures as `ProblemDetails` — integration
- Orders of another company never appear — integration

## Out of scope

- The detail page — step 5. The dashboard — step 6. The pages themselves — step 7.
- Sorting chosen by the user, the Table and Pipeline views, CSV export.

## Open questions

1. **Tabs and filters together.** Do the counters follow the Hub / Date / Status filters and the
   search, or are they totals? Does a draft count in the tab of its type? Is "All" really all, or
   all except drafts?
2. **The Date filter.** Presets (`Today`, `This week`, `Last 30 days`) resolved by the server in the
   hub's zone, or `from` / `to` sent by the page? A draft may have no Scheduled date — filter on
   `COALESCE(scheduled_at, created_at)` or leave drafts out of a date filter?
3. **The Hub filter.** Does an order transferred Markham → Toronto appear under Toronto?
4. **The Status filter of the mockup** offers only "New" and "In progress". Add Completed?
5. **The "Next" line.** The rule for each case: waiting for the truck, loading, upload a photo,
   paid, closed. Which wins when two apply, and which invoice when an order has several?
6. **A shipment row** shows "9 pallets": declared, or actual once the shipment is received?
7. **When does a card show "20 decl · 18 actual"** instead of the split by Unit Type: whenever a
   Quantity delta is known?
8. **Default sort.** Scheduled date descending, then id? Or most recently changed first?
9. **Search scope.** Number and Ref N only, or also carrier and destination?
10. **Time in the response.** UTC instants plus the hub's zone id, formatted by the page — or
    strings formatted by the server?
11. **`/api/v1/me`**: here, in step 6, or folded into each response?
12. **FastEndpoints specifics** — the command bus middleware, versioning, the `ProblemDetails`
    setup — are to be checked against its current documentation and licence, not recalled.
13. **Data volume for `EXPLAIN`.** A generated set of some hundred thousand orders, loaded only
    for the measurement?
