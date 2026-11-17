# 03 — Seed

## Goal

An empty database is filled with sample data when the API starts with seeding switched on: the
reference data, ten scripted orders that the screens and the demo rely on, and sixty completed
orders of history. Every order is built through the `Order` aggregate of [step 2](02-domain.md), so
the data cannot contradict a domain rule. There is exactly as much of it as a reviewer needs to
click through every tab, filter, search, page and chart period and never see an empty result.

The mockup is the starting point, not the target: where it contradicts itself
([`mockup-analysis.md`](../../assignment/mockup-analysis.md)) or the demo needs something else, the
data below wins.

## Decisions

- **Built through the aggregate, at startup** —
  [ADR 0008](../adr/0008-sample-data-built-through-the-domain.md). A refused domain call throws and
  stops the process, like a failed migration.
- **Only into an empty database, and only when asked.** The seed runs when `Seed:Enabled` is `true`
  and the database has no company. The setting is absent from `appsettings.json`, so the default is
  off: the API never writes sample data into a database unless its environment says so. It is on in
  `appsettings.Development.json`, in `docker-compose.yml` and, in step 9, in the deployed
  environment.
- **No reseed in the application.** Fresh data means an empty database: `docker compose down -v`
  locally, resetting the schema on the deployed database (step 9 names the command). A path that
  deletes data does not belong in the API.
- **Dates are relative to the anchor day `D` — the day before the seeding day**, in the time zone
  of the hubs; times of day are fixed. The mockup's "today" (17 Apr 2026) is `D`. If it were the
  seeding day itself, an API started in the morning would hold Operations dated later that day, and
  the first write to such an order would be rejected: the aggregate requires `now >= UpdatedAt`.
  With `D` every event is in the past whatever the hour; only a Scheduled date may lie ahead.
- **The clock is injected.** `TimeProvider.System` is registered in this step; the seed is its
  first consumer and tests replace it with `FakeTimeProvider`.
- **Deterministic.** No randomness; everything in the history follows from the index of the order.
  The same seed on the same day gives the same data, ids aside.
- **Small on purpose.** Seventy orders. The large data set that makes the query plans of step 4
  meaningful is generated separately in that step and never reaches the deployed database.
- **The seed prepares the stages of the write-flow demo** (step 8), because the API has one write
  endpoint and cannot build them itself — see *Stages*.
- **Money is realistic.** Supply prices come from the price list below. An invoice amount is the
  supply subtotal plus `14.00` for every unit of a billable Operation; Unloading and Loading are
  billable, Disposal and Restack are not. The model has no rates: the rate is a named constant of
  the seed.
- **One `SaveChangesAsync`** writes everything — EF Core wraps it in a transaction, so a failure
  leaves the database empty. Two instances seeding at once do not both succeed: the unique index on
  `hubs.name` fails the second and it exits; started again — by the platform, or by hand under
  Compose, which has no restart policy — it finds the data.

## Contract

### Code

```
src/Freitty.Infrastructure/Persistence/Seed/
├── DatabaseSeed.cs     SeedDatabaseAsync, the extension Program.cs calls: reads the switch
├── DatabaseSeeder.cs   the emptiness check, the build, the save, the log line
├── SeedCalendar.cs     the anchor day, and an instant from a day offset and a local time
├── ReferenceData.cs    company, users, hubs, carriers, services, supply items
├── OrderScript.cs      builds one order; holds the rules every seeded order follows
├── DueInvoice.cs       when an order's invoice is issued and paid, for Invoices to number
├── SeedResults.cs      turns a failed Result into an exception
├── ScriptedOrders.cs   the ten orders of the table below
├── OrderHistory.cs     the sixty completed orders
└── Invoices.cs         the 62 invoices, numbered across both groups of orders
```

- `DatabaseSeed` mirrors `DatabaseMigration`: a static extension on `IServiceProvider` that opens a
  scope. It reads the switch with `IConfiguration.GetValue<bool>("Seed:Enabled")` — absent is
  `false`, a value that is not a boolean throws — and, when on, resolves `DatabaseSeeder`. There is
  no options class for one flag; the key is a named constant.
- `DatabaseSeeder` is a sealed class registered as scoped in `AddInfrastructure`, with a primary
  constructor: `AppDbContext db`, `TimeProvider clock`, `ILogger<DatabaseSeeder> logger`.
- `Program.cs`: migrate → `await app.Services.SeedDatabaseAsync(app.Lifetime.ApplicationStopping)`
  → listen. `builder.Services.AddSingleton(TimeProvider.System)` is added there.
- `SeedCalendar` is created from `TimeProvider` and the zone `America/Toronto`, which both hubs
  use. `D` is the current date in that zone minus one day. An instant is `D` plus a day offset at a
  local time, converted with the zone's rules. All times used are daytime, clear of the night hours
  in which daylight saving skips or repeats an hour.
- A domain call that returns a failed `Result` becomes an `InvalidOperationException` carrying the
  message of the `DomainError`.
- Log, at `Information`: `Seeded sample data: {OrderCount} orders, anchor day {AnchorDay:yyyy-MM-dd}` or
  `Sample data skipped: the database is not empty`. Nothing is logged when seeding is off.

| Where | Added |
|---|---|
| `Freitty.Infrastructure` | `Microsoft.Extensions.Configuration.Binder`, 10.0.x — `GetValue<T>` lives there |
| `Freitty.Api.IntegrationTests` | `Microsoft.Extensions.TimeProvider.Testing`, the latest stable version that supports `net10.0`, looked up |
| `appsettings.Development.json` | `"Seed": { "Enabled": true }` |
| `docker-compose.yml` | `Seed__Enabled: "true"` on the `api` service |
| `README.md` | A "Sample data" section: what is seeded, when, and how to start over |

### Reference data

| What | Rows |
|---|---|
| Company | R-way Transport, balance `4250.00` |
| Users | User 1 (Admin); User 2, User 3, User 4 (Dispatcher) |
| Hubs | Markham (`Mark`, `ON`), Toronto (`Tor`, `ON`); both `America/Toronto` |
| Carriers | Schneider (`+1 800 555 0142`), TForce (`+1 800 555 0177`), R-way Transport Inc. (`+1 647 555 0199`) |
| Services | Transload, Restock & Rework |

Supply items — SKU, unit price:

| Category | Items |
|---|---|
| Securement | Straps 12 `18.50` · Straps 24 `32.00` · Ratchet strap 2in `14.75` · Load bar `42.00` |
| Edge protect | Corners 50 `12.00` · Corners 100 `21.50` · Edge board 48in `9.25` |
| Wrap | Shrink wrap 120g `24.00` · Shrink wrap 80g `17.50` · Stretch film hand roll `11.00` · Pallet cover `6.50` |
| Pallets | Pallet 48x40 `16.00` · Pallet XL `27.00` |
| Dunnage | Air bag `8.75` · Slip sheet `3.20` |
| Labels | Label roll 500 `13.40` |

People who are not Users appear as plain names: the driver "User 5", the floor leads "User 6"
(Markham) and "User 7" (Toronto).

### Scripted orders

`D−5 09:10` is 09:10 local time five days before the anchor day; `D+1` is the seeding day. A time
without a day continues the day before it. Rules for every seeded order, scripted or history:

- A draft is created and filled in the order written, every call carrying the instant of the
  draft; a scheduled date written there is a value, not that instant. "Submitted" is the `Submit`
  call with its own instant. Each later event carries the instant written before it as `now`.
- An order that gets an official number starts with any draft number — `Submit` replaces it before
  anything is saved.
- Every arrival is followed two minutes later by a dock assignment. Dock and bay are the
  implementer's choice, except for FR001383; the floor lead is the one of the hub.
- An Unloading names the trailer of the shipment's arrival. Unloadings and Loadings are billable.
  Photo and comment counts are zero unless written.
- Invoice numbers are stored as six digits; the `#` of the screens is presentation.

| Order | Script | State to assert |
|---|---|---|
| **FR001383** · Cross-Dock Storage · Markham · User 2 | Draft `D−2 15:00`: REF-1012, 10 Std; scheduled `D 08:30`; Kingston, ON; carrier R-way Transport Inc.; services Transload, Restock & Rework; submitted `D−2 15:20`. `D 08:40` arrival DryVan53 TRK-4521 / TRL-8830 · `08:42` dock `12`, bay `B` · `08:55` Unloading 12 Std, 4 photos · `09:00` warehouse note *Counted 12 pallets on arrival, BOL says 10. 1 pallet damaged → routed to Disposal.* · `09:05` receiving finished · `09:10` Disposal 1 Std, 2 photos, 1 comment · `09:25` Restack 11 Std, 1 photo · `09:30` supplies Straps 12 ×4, Corners 50 ×16, Shrink wrap 120g ×2 · `10:40` Loading 6 Std onto TRL-8830, no photo | In progress; declared 10, actual 12, On stock 5; open Quantity delta alert; supply subtotal `314.00` |
| **FR001676** · Consolidation · Markham · User 1 | Draft `D−7 10:00`: 1 REF-1001 9 Std, 2 REF-1003 6 Std, 3 REF-1002 3 XL; scheduled `D−5 09:00`; Toronto, ON; own driver User 5; submitted `D−7 10:30`. Shipment 1: `D−5 09:10` arrival DryVan53 TRK-2210 / TRL-5102, `09:30` Unloading 9 Std, 2 photos, `09:45` received. Shipment 2: `D−5 13:00` arrival DryVan53 TRK-2214 / TRL-5117, `13:20` Unloading 6 Std, 1 photo, `13:30` received. Shipment 3: `D−4 10:00` arrival Flatbed48 TRK-3075 / TRL-6140, `10:25` Unloading 3 XL, 1 photo, `10:35` received. Loadings: `D−1 15:00` 9 Std of shipment 1 and `15:20` 4 Std of shipment 2 onto TRL-7001; `D 09:30` 3 XL of shipment 3 onto TRL-7002 | In progress; declared 15 Std + 3 XL; On stock 2 Std; two outbound trailers; no Alert |
| **FR001674** · Consolidation · Markham · User 3 | Draft `D−7 09:00`: 1 REF-1005 11 Std, 2 REF-1006 9 Std; scheduled `D−4 11:00`; Calgary, AB; carrier TForce; submitted `D−7 09:20`. Shipment 1: `D−4 11:05` arrival DryVan53 TRK-1880 / TRL-4410, `11:30` Unloading 11 Std, 2 photos, `11:40` received. Shipment 2: `D−3 14:00` arrival DryVan53 TRK-1893 / TRL-4432, `14:25` Unloading 7 Std, **no photo**, `14:40` received. `D−2 10:00` Loading 11 Std of shipment 1 onto TRL-7003 | In progress; declared 20, actual 18; shipment 2 has an open Missing photo alert and an open Quantity delta alert; On stock 7; one outbound trailer |
| **FR001677** · Cross-Dock Storage · Toronto · User 4 | Draft `D−5 11:00`: REF-1013, 14 Std; scheduled `D−1 10:00`; Windsor, ON; carrier Schneider; submitted `D−5 11:15`. `D−1 10:10` arrival Reefer53 TRK-5521 / TRL-9034, `10:40` Unloading 14 Std, 3 photos, `10:50` received | In progress; On stock 14; no Alert |
| **FR001678** · Cross-Dock Pickup · Markham · User 3 | Draft `D−4 14:00`: REF-1014, 8 XL; scheduled `D 13:00`; Hamilton, ON; self pickup; submitted `D−4 14:10`. `D 13:10` arrival Flatbed48 TRK-6102 / TRL-9120 | In progress; nothing unloaded |
| **FR001679** · Cross-Dock Storage · Markham · User 1 | Draft `D−3 09:00`: REF-1015, 18 Std and 2 XL; scheduled `D+1 10:00`; Montreal, QC; carrier TForce; service Transload; submitted `D−3 09:15` | Ready; scheduled on the seeding day |
| **FR001681** · Cross-Dock Storage · Toronto · User 2 | Draft `D−1 16:00`: REF-1004, 23 Std; scheduled `D+2 14:00`; Detroit, MI; carrier Schneider; submitted `D−1 16:15` | Ready |
| **FR001672** · Cross-Dock Pickup · Markham · User 1 | Draft `D−9 10:00`: REF-1007, 10 XL; scheduled `D−3 17:30`; Brampton, ON; self pickup; submitted `D−9 10:10`. `D−3 17:35` arrival Flatbed48 TRK-4407 / TRL-8215, `17:55` Unloading 10 XL, 2 photos, `18:05` received. `D−2 09:30` Loading 10 XL onto TRL-8301, `09:45` completed. Invoice `001812` issued `D−2 10:00`, paid `D−1 12:00` | Completed; invoice `280.00`, paid |
| **FR001668** · Consolidation · Markham → Toronto · User 4 | Draft `D−10 09:00`: 1 REF-1008 15 Std, 2 REF-1009 8 Std, 3 REF-1010 20 Std, 4 REF-1011 12 Std; scheduled `D−6 08:00`; Ottawa, ON; carrier TForce; submitted `D−10 09:30`; `D−9 11:00` transferred to Toronto. On `D−6` the shipments arrive at `08:10`, `09:40`, `11:10`, `13:10` (DryVan53, TRK-3301…3304 / TRL-6201…6204); each is unloaded in full 20 minutes after its arrival with 1 photo and received 10 minutes later. `D−5`: Loadings of shipments 1 and 2 onto TRL-7004 at `10:00` and `10:15`, of 3 and 4 onto TRL-7005 at `10:30` and `10:45`; `16:00` completed. Invoice `001809` issued `D−5 16:30`, **not paid** | Completed; declared and actual 55 Std; transfer hub Toronto; invoice `1540.00`, the only unpaid one |
| **DRAFT-003** · Consolidation · Markham · User 1 | Draft `D−1 16:30`; scheduled `D+3 09:00`. Nothing else | Draft; no shipment, destination or transport |

### History

Sixty completed orders, index 0 the oldest. Order `i` is completed on day `D − offset[i]`, with the
offsets taken from this list read backwards (95 first):

```
 3  4  6 |  7  8  8 10 11 12 13 | 14 15 17 18 20 | 21 22 22 24 25 26 27        22 orders
32 34 | 35 36 36 38 40 41 | 43 44 46 48 | 49 50 50 52 53 55 | 56 57            20 orders
62 | 63 65 66 68 69 | 71 72 74 76 | 77 78 79 80 81 83 | 88 95                  18 orders
```

The list is the contract: with the two completed scripted orders it gives 24 completed in the last
30 days and 20 in the 30 before, leaves the boundary days empty so both counts hold on the seeding
day and the day after, puts a different number in neighbouring weeks, and reaches back more than
92 days so the chart has at least two quarters.

Timeline of a history order, `C` being its completion day:

- created `C−4 09:00` with its Services, submitted `C−4 09:30`, scheduled on `C−1` at a full hour
  between 08:00 and 14:00;
- the first shipment arrives 10 minutes after the scheduled instant, gets its dock two minutes
  later, is unloaded 30 minutes after it arrives and received 15 minutes after that; further
  shipments of a Consolidation follow 90 minutes apart;
- `C 10:00` Supply lines, `C 11:00` Loading of everything On stock — one outbound trailer per
  order — `C 14:00` completed, `C 15:00` invoice issued, paid at `C+2 12:00`.

What must hold; the tables behind it are the implementer's, taken by index and never random:

- `i % 4 == 3` is a Consolidation; their shipment counts cycle two, three, four. The rest are
  Cross-Docks, Storage and Pickup alternating. `i % 3 == 2` is handled at Toronto, the rest at
  Markham.
- Creators, transport, destinations, cargo, services and supplies cycle through fixed tables, so
  that across the history every User, every Carrier, every transport kind, both Unit Types, all
  three trailer types and both Services occur, about half of the orders have Supply lines, and at
  least eight different Supply items are sold. Destination regions are two-letter codes: the
  column holds three characters and the domain does not check the length.
- Orders 5, 16, 29, 40 and 53 — Cross-Docks, at both hubs — unload a quantity that differs from
  the declared one, an excess of one and a shortage of two alternating. The Quantity delta alert
  this raises is resolved one minute after it is raised; the Loading takes what was actually
  received.
- Every Unloading has at least one photo: the history raises no Missing photo alert.
- Order numbers are `FR001301 + 6·i` (the last is `FR001655`). Ref Ns run from `REF-0901` upward in
  creation order and stay below `REF-1001`. Truck and trailer numbers come from counters.
- Every history order and both completed scripted orders have exactly one invoice, with the amount
  of the rule under *Decisions*. `Invoices` collects all 62, sorts them by `IssuedAt` — two issued
  at the same instant, by order number — and numbers them `001751` to `001812`. That puts `001809`
  on FR001668 and `001812` on FR001672.

### The whole data set

On the seeding day and the day after:

| | |
|---|---|
| Orders | 70: 1 Draft, 2 Ready, 5 In progress, 62 Completed |
| Completed in the last 30 days / in the 30 before | 24 / 20 |
| Orders with an open Alert | 2 — FR001674 and FR001383 |
| Invoices | 62, one of them unpaid (FR001668) |
| Each hub | has Ready, In progress and Completed orders |

No instant other than a Scheduled date is later than `D 19:00`. Order numbers and Ref Ns are unique
across the seed. Numbers ascend with submission inside the history only; nothing sorts by number.

### Stages

What later steps may rely on:

- **Step 4** — one company; "User 1" is its only Admin.
- **Step 6** — DRAFT-003 and the unpaid invoice of FR001668 are the candidates for "awaiting your
  action".
- **Step 8** — FR001383 has 5 Standard On stock: a Loading of 5 empties it, a Loading of 6 is
  refused. FR001674-2 is received two short with its Quantity delta alert open: an Unloading of 2
  resolves that Alert. Its Missing photo alert stays — no endpoint resolves one — so the order
  still has an open Alert afterwards and the count of such orders stays 2. FR001681 and FR001679
  are Ready: any Operation starts them, an Unloading
  above the declared quantity or without a photo raises an Alert.

## Acceptance criteria

- [ ] `docker compose down -v && docker compose up -d --build --wait` exits 0, and
      `SELECT status, count(*) FROM orders GROUP BY status` returns 1, 2, 5 and 62.
- [ ] After `docker compose restart api` the counts are the same and the log says the sample data
      was skipped.
- [ ] With `Seed:Enabled` absent, the API starts on an empty database and leaves it empty.
- [ ] Every row of *Scripted orders* reads back with the state of its last column.
- [ ] The figures of *The whole data set* hold.
- [ ] The seed code reads the time only from `TimeProvider`, uses no `Random`, and changes orders
      only through the public API of `Order` and `Invoice`.
- [ ] `dotnet build Freitty.slnx -c Release` has 0 warnings; `dotnet format --verify-no-changes`
      exits 0; `dotnet test` is green, and no test outside the seed tests sees a seed row.
- [ ] Both CI jobs are green; the `stack` job now starts the API with the seed on.

## Tests

All integration, in `Freitty.Api.IntegrationTests/Seed/`. The API is started with the seed on and a
`FakeTimeProvider` on a database created for these tests (`PostgresFixture.CreateDatabaseAsync`),
seeded once and shared by the read-only tests. The clock stands at `2026-04-18T13:00:00Z` — 09:00
in Toronto — so `D` is 17 Apr 2026, the mockup's own day. `ApiFactory` gains a way to switch the
seed on and to replace `TimeProvider`.

There is still no endpoint, so the tests read through `AppDbContext` as the step 2 tests do. Where
a computed member is asserted — On stock, actual quantity, supply subtotal — the whole aggregate is
loaded with the existing `AggregateLoading` helper: on a projection those members return zero.
Counts are plain queries.

- Seeding an empty database creates the reference data: 1 company, 4 users, 2 hubs, 3 carriers,
  2 services, 16 supply items.
- The orders are 70, with the status counts, the two orders with an open Alert and the 62 invoices
  of *The whole data set*; 24 have `completed_at` in `[clock − 30 d, clock)` and 20 in
  `[clock − 60 d, clock − 30 d)`.
- FR001383 reads back with its four Operations, declared 10, actual 12, On stock 5, an open
  Quantity delta alert, two Services and a supply subtotal of `314.00`.
- FR001674 has both Alerts open on its second shipment; FR001676 has 2 Standard On stock and two
  outbound trailers.
- FR001681 is Ready with a Scheduled date after the clock; DRAFT-003 has no shipment.
- FR001672 has the paid invoice `001812` of `280.00`; the only unpaid invoice is the `1540.00` of
  FR001668, which has Toronto as its transfer hub.
- Every completed order has exactly one invoice, and its amount is the supply subtotal plus the
  rate for its billable units.
- Each hub has Ready, In progress and Completed orders; every User, transport kind, Carrier, Unit
  Type, trailer type, Cross-Dock kind and Service is used; at least eight Supply items are sold;
  Ref Ns are unique.
- Every arrived shipment has a dock assignment.
- No instant except a Scheduled date is later than the clock — on a database of its own, seeded
  with the clock at `2026-04-18T04:05:00Z`, five minutes past midnight in Toronto.
- A second start on a seeded database adds nothing.
- With the seed off, an empty database stays empty after the API starts.

## Out of scope

- Endpoints that read the data — steps 4–6. How the current user is found — step 4.
- The large data set for `EXPLAIN ANALYZE` — step 4.
- Resetting the deployed database, and whether to do it before the presentation — step 9.
- A reseed switch, a scheduled refresh, a second company.
