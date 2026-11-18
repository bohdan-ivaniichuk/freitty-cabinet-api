# 04 — Order List

## Goal

The Order List screen gets its data from the API: cards with tabs, filters, search and pagination,
all executed in PostgreSQL, and the counters of the tabs. This is the first endpoint, so the API
framework, the handler pipeline, the error contract and OpenAPI are wired here, together with the
small endpoints every page needs (`/me`, `/hubs`), and the first performance indexes arrive with
their query plans.

## Decisions

**Wired in this step**

- **FastEndpoints under `/api/v1`, handlers on its command bus** —
  [ADR 0009](../adr/0009-fastendpoints-and-its-command-bus.md). An endpoint binds the request,
  executes a query or command, and sends the result. Queries, commands, handlers and validators
  live in Application and know nothing of HTTP; the pipeline and the endpoints live in Api.
- **The pipeline is logging → validation → handler**, as command middleware that every query and
  command passes. Validation runs the FluentValidation validator of the query (rule 5); a failure
  never reaches the handler.
- **Every error is a `ProblemDetails`** (`application/problem+json`): a validation or binding
  failure is `400` with the field errors, an unknown route `404`, an unhandled exception `500`
  without details of the exception. The mapper from a `DomainError` to a `ProblemDetails` arrives
  with its first use, in step 5.
- **OpenAPI with Scalar.** The document is generated from the endpoints (`/openapi/v1.json`); the
  Scalar UI is served at `/scalar`. No Swagger UI. Both are on in every environment: the deployed
  demo shows them.
- **Enums travel as strings** (rule 11) through `JsonStringEnumConverter`; the OpenAPI schema shows
  the names.
- **`IAppDbContext` in Application**, implemented by `AppDbContext`: the `DbSet`s that exist today
  and `SaveChangesAsync`. No `DbSet` is added: children of `Order` are read through
  `db.Orders … SelectMany(order => order.Shipments)`, which translates, and invoices through
  `db.Invoices`.
- **The current user is an abstraction** — `ICurrentUser` with
  `ValueTask<CurrentUser> GetAsync(CancellationToken)`, `CurrentUser` being the user id and the
  company id. There is no authentication, so the implementation, scoped, returns the first user
  with the `Admin` role (`ORDER BY id`) and remembers it for the request; without one it throws,
  naming `Seed:Enabled`. Every query is scoped by company from the first day; authentication later
  replaces the implementation, not the queries.
- **One reporting time zone** — [ADR 0010](../adr/0010-one-reporting-time-zone.md).
  `Cabinet:TimeZone` (`America/Toronto`) in `appsettings.json`. `ReportingCalendar` in Application
  turns a date preset into a half-open range of UTC instants from `TimeProvider` and that zone.
- `Microsoft.EntityFrameworkCore.Database.Command` goes back to `Information` in
  `appsettings.Development.json`, so `dotnet run` shows the SQL (promised in step 1).
- **Startup migrations take turns.** With a second migration, instances that start together would
  both apply it: the provider's lock is released at each migration's commit, while the list of
  pending migrations is read once. `MigrateDatabaseAsync` therefore holds a PostgreSQL advisory
  lock around the whole run. The API no longer creates a missing database — Compose, the tests
  and the deployment do. This amends a consequence of
  [ADR 0007](../adr/0007-migrations-applied-at-startup.md), which trusted the provider's lock.

**The list**

- **Tabs are filters.** All, Cross-Dock, Consolidation, Alerts and Drafts are `type`, `alerts`
  and `status` values; "All" really is all, drafts included, and a draft counts in the tab of its
  type.
- **The counters follow the filters.** A tab's counter is the `total` the list would return for
  that tab with the current hub, date, status and search — so a counter never disagrees with the
  list under it. The Drafts counter ignores the status filter: it is a status itself. One
  `OrderFilter` applies those four to a query and is used by the list and by the counters; the
  agreement rests on that.
- **No date filter by default.** Today, This week and Last 30 days are presets resolved by the
  server on the Scheduled date, in the reporting zone. An order without a Scheduled date matches
  no preset.
- **The Hub filter matches the hub that handles the order**, not its transfer hub.
- **Search matches the order number and the Ref N of any shipment**, case-insensitively, as a
  `UNION` of order ids — with trigram indexes that is two index scans, where
  `number ILIKE … OR EXISTS (…)` reads every order of the company. A sub-order label typed in full
  (`FR001676-2`) finds its order. `ILIKE` is PostgreSQL's, so the search sits behind
  `IOrderSearch` in Application and is implemented in Infrastructure: it returns an
  `IQueryable<Guid>` of order ids that the handler composes into its own query, one statement in
  all. Application still does not reference the provider
  ([ADR 0001](../adr/0001-light-clean-architecture.md)).
- **Pagination is offset / limit with a total.** Every sort ends with `id`, so pages never repeat
  or skip a row. Keyset pagination is the scale-up path, named in the presentation.
- **Default sort: Scheduled date, newest first, then `id` descending.** Orders without a Scheduled
  date — drafts the client has not finished — come first: that is where PostgreSQL puts `NULL` in
  a descending order, the list can lead with what still needs the client, and the sort then
  matches an index EF Core can declare. `NULLS LAST` cannot be asked for from LINQ.
- **A card is read in two steps.** First the page: filter, sort, `OFFSET` / `LIMIT` and `COUNT`
  over `orders`. Then, for the ids of that page only, the shipments with their sums. Nothing is
  filtered, sorted or paged in memory, and no entity is loaded.
- **Sums in SQL, arithmetic in one class.** Declared, unloaded, disposed and loaded quantities are
  `GROUP BY` sums per shipment and Unit Type. `ShipmentQuantities` turns them into actual, known
  delta and On stock by the definitions of [`CONTEXT.md`](../../CONTEXT.md). The list, the detail
  and the dashboard cards all use it; the computed members of the aggregate are for the write side
  only ([step 2](02-domain.md)).
- **The "Next" line is a code with its data**; the page owns the wording. `OrderProgress` derives
  the stage of the freight and, from it, the `next` code; step 5 returns the stage itself.
- **Instants are UTC**; the hub's IANA zone travels with them and the page formats local time.
- **Initials are derived from the name, not projected.** `User.Initials` is a computed property;
  projecting it would make EF Core load the whole user. The projection takes the name and the
  initials come from one static function shared with `User` — exposing it is the only change to
  the domain in this step.

**Indexes**

- `pg_trgm` GIN indexes on `orders.number` and `shipments.ref_number`; a B-tree on
  `orders (company_id, scheduled_at DESC, id DESC)` for the default list. Each comes with
  `EXPLAIN (ANALYZE, BUFFERS)` before and after in `docs/explain/`, on a generated data set large
  enough for the planner to care — the seed's seventy orders are not.

## Contract

### `GET /api/v1/orders`

| Parameter | Values | Meaning |
|---|---|---|
| `type` | `CrossDock`, `Consolidation` | The Cross-Dock and Consolidation tabs |
| `status` | `Draft`, `Ready`, `InProgress`, `Completed` | The Status filter; `Draft` is the Drafts tab |
| `alerts` | `true`; `false` is the same as absent | The Alerts tab: orders with an open Alert on any shipment |
| `hub` | a hub id | The Hub filter |
| `date` | `Today`, `ThisWeek`, `Last30Days` | The Date filter on the Scheduled date |
| `q` | text, 3–50 characters after trimming; absent, empty or white space is no search | Search |
| `page` | 1–10 000; default 1 | The upper bound is a named constant: it keeps `OFFSET` inside an `int` |
| `pageSize` | 1–50; default 12 | |

**Date presets**, in the reporting zone, each a half-open range `[from, to)` of UTC instants with
offset zero:

| Preset | From | To |
|---|---|---|
| `Today` | start of today | start of tomorrow |
| `ThisWeek` | start of this week's Monday | start of next week's Monday |
| `Last30Days` | start of today − 29 days | start of tomorrow |

**Search.** `q` is trimmed; `%`, `_` and `\` in it are escaped and match themselves —
`EF.Functions.ILike` with its escape argument, since the two-argument form disables escaping. The
result is the union of three branches, each restricted to the current company:

1. orders whose number contains `q`;
2. orders that have a shipment whose Ref N contains `q`;
3. only when `q` ends in a hyphen and digits — split at the last hyphen — the order whose number
   equals the part before it, case-insensitively, and that has a shipment at that position. Digits
   that do not fit an `int` leave this branch out.

Three characters is the minimum because a trigram index cannot serve a shorter pattern.

Response — the card of FR001674 from the seed, abbreviated:

```jsonc
{
  "items": [{
    "number": "FR001674",
    "type": "Consolidation",
    "crossDockKind": null,
    "status": "InProgress",
    "openAlertTypes": ["QuantityDelta", "MissingPhoto"],
    "createdBy": { "name": "User 3", "initials": "U3", "role": "Dispatcher" },
    "hub": { "name": "Markham", "shortName": "Mark", "timeZone": "America/Toronto" },
    "transferHub": null,
    "scheduledAt": "2026-04-13T15:00:00+00:00",
    "destination": { "city": "Calgary", "region": "AB" },
    "transport": { "kind": "Carrier", "carrierName": "TForce", "driverName": null },
    "declared": [{ "unitType": "Standard", "quantity": 20 }],
    "declaredTotal": 20,
    "actualTotal": 18,
    "hasQuantityDelta": true,
    "outboundTrailers": 1,
    "shipments": [
      { "label": "FR001674-1", "refNumber": "REF-1005", "declaredQuantity": 11,
        "actualQuantity": 11, "openAlertTypes": [] },
      { "label": "FR001674-2", "refNumber": "REF-1006", "declaredQuantity": 9,
        "actualQuantity": 7, "openAlertTypes": ["QuantityDelta", "MissingPhoto"] }
    ],
    "next": { "code": "UploadPhoto", "invoiceNumber": null, "amount": null }
  }],
  "page": 1, "pageSize": 12, "total": 70
}
```

| Field | Definition |
|---|---|
| `openAlertTypes` | the distinct types of open Alerts — of the shipment, or of all shipments for the order — in the order of the enum's values |
| `transferHub` | `{ "name", "shortName" }` or `null` |
| `scheduledAt`, `destination`, `transport` | `null` in a draft that has not set them |
| `transport` | `kind`, and `carrierName` or `driverName` as the kind has one |
| `declared` | the declared quantity per Unit Type over all shipments, `Standard` first; empty without cargo |
| `declaredTotal`, `actualTotal` | sums over all shipments and Unit Types |
| `hasQuantityDelta` | some shipment has a known, non-zero delta for some Unit Type — an excess at once, a shortage once it is received |
| `outboundTrailers` | the number of distinct trailer numbers of Loadings |
| `shipments` | ordered by position; `label` is `<number>-<position>` for every shipment, a Cross-Dock's and a draft's included; `declaredQuantity` and `actualQuantity` are sums over Unit Types |

**`OrderProgress`.** The stage of the freight, the first that applies:

| Stage | When |
|---|---|
| `Draft` | status `Draft` |
| `Completed` | status `Completed` |
| `AwaitingTruck` | no shipment has arrived, none has an Operation and none is received |
| `Unloading` | some shipment is not received |
| `Loading` | every shipment is received and something is On stock |
| `AwaitingCompletion` | every shipment is received and nothing is On stock |

`next.code`, the first that applies:

| Code | When | Data |
|---|---|---|
| `ContinueEditing` | stage `Draft` | |
| `UploadPhoto` | an open Missing photo alert | |
| `ReviewQuantityDelta` | an open Quantity delta alert | |
| `AwaitingTruck`, `Unloading`, `Loading`, `AwaitingCompletion` | the stage of that name | |
| `AwaitingPayment` | stage `Completed` with an unpaid invoice | the unpaid invoice issued first (`issued_at`, then `id`): number and amount |
| `Paid` | stage `Completed`, invoiced, nothing unpaid | the invoice issued last: number and amount |
| `Closed` | stage `Completed` without an invoice | |

### `GET /api/v1/orders/counts`

Takes `hub`, `date`, `status` and `q` with the meaning above. Returns
`{ "all", "crossDock", "consolidation", "alerts", "drafts" }` — for each tab, the `total` of the
list for that tab under those filters; `drafts` ignores `status`.

Two statements. One groups the orders that pass `hub`, `date` and `q` by type and status — eight
rows at most — and the handler folds them: a tab adds the rows of its type and of the requested
status, Drafts adds the rows of status `Draft`. The other counts the orders that pass all four
filters and have an open Alert.

### `GET /api/v1/hubs`

`[{ "id", "name", "shortName" }]`, ordered by name — the options of the Hub filter.

### `GET /api/v1/me`

```jsonc
{ "user": { "name": "User 1", "initials": "U1", "role": "Admin" },
  "company": { "name": "R-way Transport", "balance": 4250.00 } }
```

Step 6 adds the number for the sidebar badge.

### Errors

| Case | Response |
|---|---|
| A parameter out of range, an unknown enum value, a `hub` that is not a GUID, `q` shorter than 3 or longer than 50 characters after trimming | `400` with the field errors |
| An unknown route | `404` `ProblemDetails` |
| No user to act as (an empty, unseeded database) | `500` `ProblemDetails`; the log line says to seed the database |

A `hub` id that matches no hub is not an error: the list is empty. An empty `hub=` is a `400` —
it would otherwise bind to the empty GUID and silently return nothing; an empty `type=`, `status=`
or `date=` is the same as absent. A control character in `q` is a `400`: PostgreSQL cannot store
one.

Instants are serialised with an explicit offset, `2026-04-13T15:00:00+00:00`.

The body of a `400` is the one FastEndpoints produces, and the pages rely on it:

```jsonc
{ "type": "…", "title": "Bad Request", "status": 400, "instance": "/api/v1/orders",
  "detail": "…the first error…",
  "errors": [{ "name": "pageSize", "reason": "…" }] }
```

`name` is the parameter as the request spells it (camelCase), whether the binder or a validator
refused it.

### Code

```
src/Freitty.Application/
├── DependencyInjection.cs   AddApplication(): the application's own services
├── Abstractions/     IAppDbContext, ICurrentUser, CurrentUser, IOrderSearch
├── Common/           ReportingCalendar, ReportingTimeZone, DatePreset
├── Orders/           OrderFilter — the hub, date, status and search filters applied to a query;
│                     the read model shared with the detail of step 5: UnitTypeSums,
│                     ShipmentQuantities, ShipmentProgress, OrderProgress, OrderStage, ShipmentLabel
├── Orders/Cards/     OrderCardDto and its parts, OrderCardReader
├── Orders/ListOrders/    ListOrdersQuery, its handler and validator, the paged result
├── Orders/CountOrders/   CountOrdersQuery, its handler and validator, OrderCountsDto
├── Hubs/ListHubs/        ListHubsQuery, its handler, HubDto
└── Me/GetMe/             GetMeQuery, its handler, MeDto
src/Freitty.Api/
├── Endpoints/…       one folder per endpoint (rule 4)
├── Pipeline/         the two command middleware classes
└── …                 FastEndpoints, ProblemDetails, OpenAPI and Scalar, JSON options, the
                      reporting zone read from configuration
src/Freitty.Infrastructure/
├── Identity/         the ICurrentUser implementation
├── Persistence/      AppDbContext : IAppDbContext; OrderSearch : IOrderSearch
└── Persistence/Migrations/   OrderListIndexes
```

- `OrderCardReader` takes a list of order ids and returns their cards in that order; the list
  handler and, in step 6, the dashboard both call it.
- `ReportingTimeZone` wraps the `TimeZoneInfo`. Api reads `Cabinet:TimeZone` while the services are
  registered, requires an IANA id the system knows — the rule of the `Hub` constructor, because
  step 6 hands the id to PostgreSQL — and otherwise throws, naming `Cabinet__TimeZone`. It
  registers the value; `ReportingCalendar(TimeProvider clock, ReportingTimeZone zone)` is resolved
  per request, so a test's clock is the one it sees.
- The health endpoints of step 1 stay as they are, outside `/api/v1`.
- [`docs/plan.md`](../plan.md) §6 is already updated to this contract; the implementer changes it
  only if the implementation ends up differing.

### Wiring

Checked against the libraries on 2026-11-18 with a scratch application; the implementer confirms
each point against the current documentation rather than trusting this list.

| Where | Package |
|---|---|
| `Freitty.Application` | `FastEndpoints.Messaging.Core` (`ICommand<T>`, `ICommandHandler<,>`; no dependencies), `FluentValidation`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `Freitty.Api` | `FastEndpoints`, `FastEndpoints.OpenApi`, `Scalar.AspNetCore`, `FluentValidation.DependencyInjectionExtensions` |

Latest stable versions, looked up (FastEndpoints 9.0.0, Scalar.AspNetCore 3.0.1 and
FluentValidation 12.2.0 at the time of writing); the FluentValidation version is the one
FastEndpoints brings, and the `Microsoft.*` packages stay on 10.0.x. `FastEndpoints.Swagger` is
the legacy NSwag path and is not used.

- **Routes.** `RoutePrefix = "api"`, `Versioning.Prefix = "v"`, `Versioning.PrependToRoute = true`
  (the default appends: `/api/orders/v1`), `Versioning.DefaultVersion = 1`. An endpoint declares
  `Get("orders")` and is served at `/api/v1/orders`.
- **A query is an `ICommand<TResult>`**, its handler an `ICommandHandler<,>`; plain
  `AddFastEndpoints()` finds handlers in the Application assembly. The endpoint calls
  `ExecuteAsync` on the query. The list and the counters bind the query record itself — the query
  is exactly what the request carries; `/hubs` and `/me` have no request and create their query.
  A separate request record appears when request and command differ (step 8).
- **Pipeline.** Two open-generic `ICommandMiddleware<,>` classes in Api, registered with
  `AddCommandMiddleware` in the order logging, validation. The logging one writes, at
  `Information`, the query's type name, the elapsed time and whether it completed, was rejected by
  validation, or threw. The validation one resolves `IValidator<TCommand>` from DI — FastEndpoints
  does not register them, so the validators of the Application assembly are registered with
  `AddValidatorsFromAssembly` — passes when there is none, and reports failures through
  `ValidationContext.Instance.AddError(…)` and `ThrowIfAnyErrors()`. Throwing FluentValidation's
  own exception would be a `500`.
- **Binding** refuses an unknown enum name, a malformed GUID or number with the same `400` before
  the pipeline runs. It accepts a defined enum number (`status=2`) as well as its name; that is
  left as it is.
- **Errors.** `c.Errors.UseProblemDetails()` for validation and binding. An unknown route and an
  unhandled exception are bodiless by default: ASP.NET Core's `AddProblemDetails()` with
  `UseStatusCodePages()` and `UseExceptionHandler()` gives them a body. The exception handler is
  the outermost middleware, before the request logging; the exception is logged once and its
  message is never in the response.
- **JSON.** `JsonStringEnumConverter` is added to `c.Serializer.Options` and to ASP.NET Core's
  `JsonOptions`, which serialise `TypedResults`. Property names are camelCase.
- **OpenAPI.** `.OpenApiDocument(…)` on `AddFastEndpoints()` with `DocumentName = "v1"` and
  `MaxEndpointVersion = 1` (the default of 0 lists no versioned endpoint), then `MapOpenApi()` and
  `MapScalarApiReference()`. `AddOpenApi()` is not called directly. Each endpoint has a summary
  and describes its responses.
- **Sending.** `Send.OkAsync(…)` and its siblings; the older `SendAsync` methods no longer exist.

### Migration `OrderListIndexes`

`HasPostgresExtension("pg_trgm")` on the model, then:

| Index | Declared as |
|---|---|
| `ix_orders_number_trgm` | `HasIndex(order => order.Number, "ix_orders_number_trgm").HasMethod("gin").HasOperators("gin_trgm_ops")` — the named overload: `orders.number` already has an index, and the unnamed one would reconfigure it |
| `ix_shipments_ref_number_trgm` | the same on `shipments.ref_number` |
| `ix_orders_company_id_scheduled_at_id` | `HasIndex(company_id, scheduled_at, id)` with `IsDescending(false, true, true)` and that name |

`docs/er-diagram.md` gets a short "Indexes" section naming them.

### Query plans

```
docs/explain/
├── README.md              what was measured, on what data, how to repeat it
├── generate-volume.sql    fills a migrated, unseeded database
├── search-number.md       ┐
├── search-ref-number.md   ├ the statement, the plan before and after, two lines on what changed
└── list-default.md        ┘
```

How:

1. A throwaway PostgreSQL 18 container on a port of its own (`docker run -d --name
   freitty-explain -e POSTGRES_PASSWORD=… -p 55432:5432 postgres:18-alpine`), removed afterwards.
   Nothing is generated into the Compose database and nothing volume-related ships in the
   application.
2. Migrate it by starting the API once against it with the seed off
   (`ConnectionStrings__Postgres=… dotnet run --project src/Freitty.Api`), then stop the API.
3. Run `generate-volume.sql` through `docker exec -i freitty-explain psql`: about 200 000 orders
   in 50 companies, with one to three shipments each, cargo lines and Operations, then
   `VACUUM ANALYZE`. Ids have no column default; PostgreSQL 18 has `uuidv7()`. The script must
   satisfy every constraint of [step 2](02-domain.md) — on `orders`: the enum lists, the
   Cross-Dock kind only on a Cross-Dock, the carrier or driver matching the transport kind, a
   transfer hub different from the hub, the milestones matching the status and in order, a
   submitted order with its Scheduled date, destination and transport, the two unique numbers; on
   `shipments`: positions from 1, unique position and Ref N per order, arrival and dock columns all
   or none; on `cargo_lines`: one line per Unit Type; on `operations`: a trailer number exactly for
   Unloading and Loading.
4. The statements are the ones EF Core sends: copied from the SQL log of `dotnet run` in
   Development while calling the endpoint, with the parameters replaced by literals for one
   generated company. The search uses a selective pattern — a whole Ref N, a whole number.
5. "Before" is measured in a transaction that drops the index and rolls back:
   `BEGIN; DROP INDEX …; EXPLAIN (ANALYZE, BUFFERS) …; ROLLBACK;`. The search is one statement
   using both trigram indexes: `search-number.md` searches by a number and drops
   `ix_orders_number_trgm`, `search-ref-number.md` searches by a Ref N and drops
   `ix_shipments_ref_number_trgm`. `list-default.md` is the page query of the default list and
   drops `ix_orders_company_id_scheduled_at_id`.
6. Also measured there and stated in `README.md`: the `OR EXISTS` form of the search against the
   `UNION` form.

## Acceptance criteria

On a freshly seeded stack — `docker compose down -v && docker compose up -d --build --wait` first,
because "today" moves:

- [ ] `GET /api/v1/orders` returns 12 items and `total` 70, in this order: DRAFT-003, FR001681,
      FR001679, FR001678, FR001383, FR001677, FR001672, then history and the remaining scripted
      orders by Scheduled date.
- [ ] `?alerts=true` returns FR001383 and FR001674; `?status=Draft` returns DRAFT-003;
      `?q=REF-1006` and `?q=fr001674-2` return FR001674 only; `?hub=<Toronto>&status=Ready`
      returns FR001681 only; `?date=Today` returns FR001679 only.
- [ ] `GET /api/v1/orders/counts` returns 70 / 51 / 19 / 2 / 1, and for every combination tried the
      counter of a tab equals the `total` of the list.
- [ ] The scripted orders' cards:

      | Order | `next` | Also |
      |---|---|---|
      | FR001383 | `ReviewQuantityDelta` | declared 10, actual 12, delta, 1 trailer |
      | FR001676 | `Loading` | 15 Standard + 3 ExtraLarge, 2 trailers |
      | FR001674 | `UploadPhoto` | the example above |
      | FR001677 | `Loading` | |
      | FR001678 | `Unloading` | |
      | FR001679 | `AwaitingTruck` | 18 Standard + 2 ExtraLarge |
      | FR001681 | `AwaitingTruck` | declared 23, actual 0 |
      | FR001672 | `Paid` | invoice `001812`, 280.00 |
      | FR001668 | `AwaitingPayment` | invoice `001809`, 1540.00; transfer hub Toronto; 2 trailers |
      | DRAFT-003 | `ContinueEditing` | no shipments; `scheduledAt` set |

- [ ] An invalid `pageSize`, enum value or `hub` is a `400` `ProblemDetails` with field errors; an
      unknown route under `/api` is a `404` `ProblemDetails`.
- [ ] `/scalar` serves the Scalar page and `/openapi/v1.json` lists the four endpoints under
      `/api/v1` with their parameters and schemas; enums are strings.

Elsewhere:

- [ ] Under `dotnet run` in Development, the SQL log of a list request shows `LIMIT` / `OFFSET`, a
      `COUNT`, and the card statements restricted to the ids of the page.
- [ ] `docs/explain/` holds the three plans; the "after" plan of each search uses its trigram
      index, and the "after" plan of `list-default` has no Sort node under its Limit.
- [ ] `dotnet ef migrations has-pending-model-changes` reports none; `docs/er-diagram.md` names the
      indexes.
- [ ] Build with 0 warnings, `dotnet format --verify-no-changes` clean, `dotnet test` green.
- [ ] *Browser session:* `/scalar` renders the four endpoints and a request sent from it succeeds.

## Tests

Integration, through HTTP (the pattern of
[ADR 0002](../adr/0002-integration-tests-on-real-postgres.md) returns with the first endpoint), in
`Freitty.Api.IntegrationTests/Orders/`.

- `ApiFactory` gains an optional `ICurrentUser`. Every HTTP test passes its own: in the shared
  database the default implementation would find some other test's Admin.
- Each test class builds its orders through the aggregate under a company and user of its own,
  with builders added to the test project as needed — a Consolidation, a completed order, an event
  at a chosen instant — and starts the API with that user and a `FakeTimeProvider`.
- Responses are read with the web defaults of `System.Text.Json` plus the string-enum converter.

What is covered:

- Each tab and each filter, alone and combined.
- The date presets at their boundaries in the reporting zone, including an order scheduled at
  23:30 local time today and one at 00:30 tomorrow; an order without a Scheduled date matches none.
- Search by order number, by Ref N, by a part of either, by a sub-order label, case-insensitively;
  a `%` or `_` in `q` matches literally; two characters are a `400`; white space is no search.
- Pagination: page boundaries, the total, the last partial page, a page past the end is empty; the
  order is stable across pages when several orders share a Scheduled date.
- The default sort, with unscheduled orders first.
- A card with a known ledger: declared per Unit Type, totals, the delta flag for an excess and for
  a shortage before and after receiving, outbound trailers, shipment rows, open Alert types.
- Each stage and each `next` code, including an order whose only shipment never arrived and was
  finished: `AwaitingCompletion`, not `AwaitingTruck`.
- The counters equal the list's totals for the same filters; `drafts` ignores `status`.
- Validation and binding failures as `ProblemDetails` with the name of the field; an unknown
  route under `/api` is a `404` with a body; the OpenAPI document lists the four endpoints.
- Orders of another company never appear, in the list, in the counters or in a search.
- `/hubs` contains the test's own hubs in name order; `/me` returns the test's user and company.
- The migration: the three indexes exist and the model has no pending changes.
- Startup fails, naming `Cabinet__TimeZone`, when the zone is missing or not an IANA id.

`ShipmentQuantities` and `OrderProgress` are pure; their cases are covered through the endpoints
above, not in a project of their own.

## Out of scope

- The detail — [step 5](05-order-detail.md). The dashboard — [step 6](06-dashboard.md). The
  pages — [step 7](07-frontend.md).
- Sorting chosen by the user, the Table and Pipeline views, CSV export.
- Authentication and authorisation.
- Searching by carrier, destination or creator.
