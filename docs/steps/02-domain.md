# 02 — Domain

## Goal

The domain model of the cabinet and its database schema: the `Order` aggregate with its rules, the
reference data around it, EF Core configurations and the first migration, applied when the API
starts. Domain rules are covered by unit tests that need no database; the mapping and the
constraints are covered by integration tests on PostgreSQL. No endpoint and no seed data yet.

The words used here are defined in [`CONTEXT.md`](../../CONTEXT.md); the tables are drawn in
[`docs/er-diagram.md`](../er-diagram.md).

## Decisions

**Model**

- `Order` is one aggregate and the only entry point for changes to it —
  [ADR 0003](../adr/0003-order-is-one-aggregate.md). Reads never load it.
- Every quantity has one home: declared in Cargo Lines, actual in the Operations ledger. Nothing
  derived is stored — [ADR 0004](../adr/0004-quantities-have-one-source.md).
- State is stored, not event-sourced —
  [ADR 0005](../adr/0005-stored-state-not-event-sourcing.md).
- A Shipment is one consignment under one bill of lading: its cargo, its arrival, its dock and its
  Operations and Alerts belong to it, not to the Order. A Consolidation receives each shipment
  separately, and a shortage has to be visible per shipment.
- An Alert is a stored record with `RaisedAt` / `ResolvedAt`, not a computed flag: when the client
  was told and when it ended cannot be rebuilt from current state. The Quantity delta itself stays
  computed.
- An excess is known the moment it is unloaded; a shortage is known only when the hub says it has
  finished receiving the shipment (`FinishReceiving`). Judging a shortage after every Unloading
  would raise a false Alert on every split unload and on every shipment with two Unit Types, and
  would never report a shipment that did not arrive at all.
- A `QuantityDelta` Alert follows changes of the delta, not its presence. Once someone has resolved
  an Alert by hand, the same delta does not raise it again; a different delta does — also a smaller
  one, because the client was told about a state that no longer holds.
- The status describes only what happens to the freight: `Draft → Ready → InProgress → Completed`.
  Payment belongs to the invoice; an Alert is not a status. Any action of the hub starts a `Ready`
  order. Milestone instants (`SubmittedAt`, `StartedAt`, `CompletedAt`) give the status as of any
  moment and feed the dashboard later. Statuses are never compared by their numbers.
- An order whose shipments never arrived is closed the same way as any other: receiving is
  finished, the Alerts are resolved, the order is completed. A separate cancelled status is out of
  scope.
- A draft is the same `Order` in status `Draft`, so the list, its filters and the tab counters read
  one table. It carries a draft number (`DRAFT-003`); `Submit` replaces it with the official one, so
  abandoned drafts do not consume official numbers. Draft numbers are the company's own; official
  numbers are unique across the platform. Number generation is out of scope: the methods take the
  number as an argument.
- The Customer of the hub's screens is the `Company` that owns the order: the detail page mirrors
  the hub's view, and to the hub the customer is whoever ordered the service. There is no separate
  customer field. `Company.Balance` is a figure kept for display — top-ups and payments are outside
  the task, so nothing here adds up to it.
- Transport is how the freight leaves the hub: a choice of kind first (`Carrier`, `OwnDriver`,
  `SelfPickup`) and a carrier only for the first kind. The carrier directory holds real carriers
  only. The vehicle recorded on a Shipment is the inbound one.
- Enum when the code has a rule about a specific value (order type, status, transport kind,
  operation type, alert type); table when the set is a catalog the business extends without a
  release (`Service`, `SupplyItem`). `UnitType` and `TrailerType` are catalogs in a real product and
  stay enums here because the assignment has a closed set of each. `…Type` classifies an entity;
  `…Kind` classifies one aspect of an order.
- `Restack` is count-neutral: it rearranges pallets and does not change what is On stock.
- Photos and comments are counters on an Operation: file storage and chat are outside the task.
- Supplies belong to the order, as the Cross-Dock detail shows them. Attaching a line to one
  shipment of a Consolidation is a nullable column later.

**Behaviour**

- The whole lifecycle is implemented now. Its consumer is the seed of step 3, which builds every
  order through the aggregate, so sample data cannot violate a rule; the write flow of step 8 reuses
  `RecordOperation`.
- A domain method receives the instant as an argument (`now`). It never reads a clock: a handler
  takes the instant from `TimeProvider`, the seed passes past dates, a test passes a literal. This
  is why the domain tests need no `FakeTimeProvider`, which rule 17 otherwise asks for.
- A refusal that depends on the state of the aggregate is a `Result` carrying a `DomainError` with a
  stable code and a message that names what is wrong — rules 2 and 6. A broken calling contract — a
  quantity of zero, an empty string, `null`, a value that is not a member of its enum, an instant
  that goes backwards — is an `ArgumentException`: such a value comes from a validator or from the
  clock, not from a decision of the user.
- A refused method changes nothing. All rules are checked before the first assignment.
- The domain stores ids of other aggregates and of reference data and never assigns a navigation to
  them. It may read an entity it is handed — a price, a status — but does not keep it. EF Core fills
  navigations when it reads, so nothing depends on what a context happens to track.
- Every successful change of the aggregate sets `Order.UpdatedAt = now` and increments
  `Order.Version`. The order row is therefore written by every change, also when two changes carry
  the same instant, and step 8 has a place for its concurrency token.
- Time moves forward within an order: every method requires `now >= UpdatedAt`. That one rule keeps
  the milestones, the Operations and the Alerts in order.
- Every entity that stores an instant — `Order`, its children and `Invoice` — stores it as
  PostgreSQL will: converted to UTC (`ToUniversalTime()`) and cut to microseconds. Npgsql refuses a
  `DateTimeOffset` whose offset is not zero, a seed naturally writes hub-local times, and an instant
  with finer ticks would not equal itself after a round trip.
- Strings are stored trimmed; an empty one is a contract violation. Truck and trailer numbers — of
  an arrival and of an Operation — are also upper-cased, because trailers are counted by distinct
  number. A Ref N is compared exactly, as the unique index does. The domain does not check maximum
  lengths: validators do at the API, and the column does for anything else.

**Persistence**

- Keys are UUID v7, created by the entity — [ADR 0006](../adr/0006-uuid-v7-keys-from-the-domain.md).
  This is the one place the domain touches the system clock; rule 12 is about business time, and
  the timestamp inside the key only orders the index.
- `snake_case` names through `EFCore.NamingConventions`, plural table names set explicitly: the
  dashboard SQL of step 6 is written by hand, and quoted identifiers make it hard to read.
- Enums are stored as integers (rule 11 already requires explicit values). Hand-written SQL receives
  them as parameters taken from the enum, never as literals.
- Instants are `DateTimeOffset` in code and `timestamptz` in the database, always UTC. `timestamptz`
  stores no zone, so `Hub.TimeZone` (IANA id) is what later steps use to show and group by hub-local
  time.
- Money is `decimal` / `numeric(12,2)` in one currency (CAD), with no currency column.
- Integrity is enforced twice wherever one row or one index can express the rule: by the aggregate,
  and by unique indexes and `CHECK` constraints, so a row written past the domain still cannot
  break it. Rules that need other rows — quantities against the ledger, the number of shipments —
  stay with the aggregate.
- The ledger is protected from deletion: Operations and Alerts are `RESTRICT`. The exact rules are
  under *Schema*. No soft delete.
- No search or filter index is added in this step; they arrive in step 4 with `EXPLAIN ANALYZE`
  before and after. The indexes EF Core creates for foreign keys stay.
- Migrations are applied by the API at startup —
  [ADR 0007](../adr/0007-migrations-applied-at-startup.md).

**Moved**

- `IAppDbContext` moves from this step to step 4, with its first consumer — the first handler. The
  seed lives in Infrastructure and uses `AppDbContext` directly.
- Setting `Microsoft.EntityFrameworkCore.Database.Command` back to `Information` in Development,
  which step 1 promised for "the step with the first query", also waits for step 4: this step runs
  queries only in tests and at startup.

## Contract

### Projects and packages

```
src/Freitty.Domain/
├── Common/      Result, Result<T>, DomainError, DomainErrorKind
├── Companies/   Company, User, UserRole
├── Hubs/        Hub
├── Carriers/    Carrier
├── Catalog/     Service, SupplyItem
├── Billing/     Invoice, InvoiceErrors
└── Orders/      Order, Shipment, CargoLine, Operation, NewOperation, Alert, SupplyLine,
                 OrderService, OrderErrors and the enums below
src/Freitty.Infrastructure/Persistence/
├── AppDbContext.cs
├── Configurations/   one IEntityTypeConfiguration<T> per entity
├── Migrations/       InitialSchema
└── DatabaseMigration.cs
tests/Freitty.Domain.Tests/   → Domain only; xunit.v3, set up like the integration test project
```

| Where | Added |
|---|---|
| `Freitty.Infrastructure` | `EFCore.NamingConventions` |
| `Freitty.Api` | `Microsoft.EntityFrameworkCore.Design`, `PrivateAssets="all"` |
| `Freitty.Domain.Tests` | `xunit.v3` |
| Repository tool manifest | `dotnet-ef` |

Latest stable versions at implementation time, looked up; `Microsoft.*` and `dotnet-ef` stay on
10.0.x. `Freitty.Domain` still has no package or project reference.

### `Result`

- `DomainError` — a sealed record: `Code`, `Message`, `Kind`.
- `DomainErrorKind` — `NotFound = 1` for the two `….not_found` refusals, `Conflict = 2` for every
  other: the state of the aggregate does not allow the change. An endpoint maps the kind to an HTTP
  status and never switches on code strings.
- `Result` — `IsSuccess`, `IsFailure`, `Error` (`null` on success); created with `Result.Success()`
  or from a `DomainError` by implicit conversion.
- `Result<T>` — the same plus `Value`, which throws `InvalidOperationException` on a failed result;
  created from a `T` or from a `DomainError` by implicit conversion.
- One refusal carries one error. `Submit` returns a single `order.not_ready_to_submit` whose message
  lists everything that is missing.
- Each error is a named member of `OrderErrors` or `InvoiceErrors` that builds the message from its
  arguments. A message names its subject: `Cannot load 15 Standard on FR001383-1: 11 on stock.`

### Enums

| Enum | Members |
|---|---|
| `OrderType` | `CrossDock = 1`, `Consolidation = 2` |
| `CrossDockKind` | `Storage = 1`, `Pickup = 2` |
| `OrderStatus` | `Draft = 1`, `Ready = 2`, `InProgress = 3`, `Completed = 4` |
| `TransportKind` | `Carrier = 1`, `OwnDriver = 2`, `SelfPickup = 3` |
| `UnitType` | `Standard = 1`, `ExtraLarge = 2` |
| `OperationType` | `Unloading = 1`, `Disposal = 2`, `Restack = 3`, `Loading = 4` |
| `AlertType` | `QuantityDelta = 1`, `MissingPhoto = 2` |
| `TrailerType` | `DryVan53 = 1`, `Reefer53 = 2`, `Flatbed48 = 3` |
| `UserRole` | `Admin = 1`, `Dispatcher = 2` |

A property is named after its column, so three nullable ones share the name of their enum:
`Order.CrossDockKind`, `Order.TransportKind`, `Shipment.TrailerType`. Inside those classes
`TransportKind.Carrier` then binds to the property and does not compile. The file refers to the
enum through a `using` alias; that is a `using`, not the inline qualified name rule 16 forbids.

### Schema

Every table has `id uuid` as its primary key. `?` marks a nullable column. A foreign key is named
for its role and ends in `_id`.

| Table | Columns | Unique | `CHECK` |
|---|---|---|---|
| `companies` | `name` varchar(200), `balance` numeric(12,2) | | |
| `users` | `company_id`, `name` varchar(200), `role` int | | |
| `hubs` | `name` varchar(100), `short_name` varchar(20), `region` varchar(3), `time_zone` varchar(64) | `name` | |
| `carriers` | `name` varchar(200), `phone`? varchar(32) | `name` | |
| `services` | `name` varchar(100) | `name` | |
| `supply_items` | `sku` varchar(100), `category` varchar(100), `unit_price` numeric(12,2) | `sku` | `unit_price >= 0` |
| `orders` | `number` varchar(20), `type` int, `cross_dock_kind`? int, `status` int, `company_id`, `created_by_id`, `hub_id`, `transfer_hub_id`?, `scheduled_at`?, `destination_city`? varchar(100), `destination_region`? varchar(3), `transport_kind`? int, `carrier_id`?, `driver_name`? varchar(200), `created_at`, `submitted_at`?, `started_at`?, `completed_at`?, `updated_at`, `version` int | (`company_id`, `number`); `number` for every order that is not a `Draft` | listed below |
| `shipments` | `order_id`, `position` int, `ref_number` varchar(50), `warehouse_note`? varchar(2000), `trailer_type`? int, `truck_number`? varchar(20), `trailer_number`? varchar(20), `arrived_at`?, `received_at`?, `dock`? varchar(20), `bay`? varchar(20), `floor_lead`? varchar(200), `dock_assigned_at`? | (`order_id`, `position`); (`order_id`, `ref_number`) | `position >= 1`; the four arrival columns (`trailer_type`, `truck_number`, `trailer_number`, `arrived_at`) are all set or all empty; so are the four dock columns |
| `cargo_lines` | `shipment_id`, `unit_type` int, `quantity` int | (`shipment_id`, `unit_type`) | `quantity > 0` |
| `operations` | `shipment_id`, `type` int, `unit_type` int, `quantity` int, `trailer_number`? varchar(20), `applied_at`, `is_billable` bool, `photo_count` int, `comment_count` int | | `quantity > 0`; `photo_count >= 0`; `comment_count >= 0`; `trailer_number` is set exactly for `Unloading` and `Loading` |
| `alerts` | `shipment_id`, `type` int, `raised_at`, `resolved_at`? | (`shipment_id`, `type`) `WHERE resolved_at IS NULL` | `resolved_at >= raised_at` |
| `supply_lines` | `order_id`, `supply_item_id`, `quantity` int, `unit_price` numeric(12,2) | | `quantity > 0`; `unit_price >= 0` |
| `order_services` | `order_id`, `service_id` | (`order_id`, `service_id`) | |
| `invoices` | `number` varchar(20), `order_id`, `amount` numeric(12,2), `issued_at`, `paid_at`? | `number` | `amount >= 0`; `paid_at >= issued_at` |

`CHECK` constraints on `orders`:

- `cross_dock_kind` is set exactly when `type` is `CrossDock`.
- `carrier_id` is set exactly when `transport_kind` is `Carrier`; `driver_name` is set exactly when
  `transport_kind` is `OwnDriver`. Both compare with `IS NOT DISTINCT FROM`, not `=`:
  `transport_kind` is nullable, a comparison with `NULL` is `NULL`, and a `CHECK` passes on `NULL` —
  the plain form would accept a carrier on an order with no transport kind.
- `transfer_hub_id` differs from `hub_id`.
- `destination_city` and `destination_region` are set together.
- Status and milestones agree: `submitted_at` is set exactly when the status is not `Draft`;
  `started_at` exactly when it is `InProgress` or `Completed`; `completed_at` exactly when it is
  `Completed`.
- Milestones are in order: `created_at <= submitted_at <= started_at <= completed_at`, each
  comparison applying when both sides are set.
- An order that is not a `Draft` has `scheduled_at`, `destination_city` and `transport_kind`.
- `version >= 1`.

Rules for all tables:

- Every enum column has a `CHECK` that lists the values of its enum. The list is built from the
  enum by one helper, so a new member changes the constraint in the next migration.
- A `CHECK` or an index filter that mentions an enum value takes the number from the enum, not from
  a literal. Constraint names: `ck_<table>_<rule>`.
- The naming convention does not touch raw SQL. Every `HasCheckConstraint` and `HasFilter` is
  written with the `snake_case` column names.
- The naming convention changes case only. Every configuration names its table with `ToTable`, or
  the children come out singular.
- Delete behaviour: `shipments`, `supply_lines`, `order_services` → `orders` and `cargo_lines` →
  `shipments` are `CASCADE`; every other foreign key is `RESTRICT`, including `operations` and
  `alerts` → `shipments`. So a draft can be deleted with everything it has, and an order with a
  ledger or an invoice cannot.
- `alerts.shipment_id` gets a plain index of its own: EF Core counts the partial unique index as
  covering the foreign key and creates none, and that index does not serve a lookup of all Alerts
  of a shipment.
- An invoice has no `company_id`: the company is the one of its order.
- The migrations history table is `__ef_migrations_history`, set with `MigrationsHistoryTable`.
- `AppDbContext` exposes `DbSet`s for `Order`, `Invoice`, `Company`, `User`, `Hub`, `Carrier`,
  `Service`, `SupplyItem`. Children of `Order` are reached through it.

### Entities

- All classes are `sealed`. Properties have private setters. Constructors and mutators of
  `Shipment`, `CargoLine`, `Operation`, `Alert`, `SupplyLine` and `OrderService` are `internal`:
  only `Order` creates and changes them.
- Collections are exposed as `IReadOnlyCollection<T>` over a private list: `Order.Shipments`,
  `Order.SupplyLines`, `Order.Services`; `Shipment.CargoLines`, `Shipment.Operations`,
  `Shipment.Alerts`.
- Each entity has a private parameterless constructor for EF Core. Each public or internal
  constructor sets `Id = Guid.CreateVersion7()`.
- `Company`, `User`, `Hub`, `Carrier`, `Service` and `SupplyItem` are reference data: a public
  constructor with the values of the table above that checks its own arguments — no empty name, no
  negative price — and no other behaviour, with two exceptions. `User.Initials` is the first
  character of each of the first two words of the name, upper-cased (`User 1` → `U1`). The `Hub`
  constructor accepts only an IANA time zone id the system knows
  (`TimeZoneInfo.TryFindSystemTimeZoneById`, then `HasIanaId`): PostgreSQL does not recognise
  Windows ids, and an id it rejects fails every query that groups by hub-local time.
- Read-only navigations exist because read projections need them: `Order.Company`, `CreatedBy`,
  `Hub`, `TransferHub`, `Carrier`; `OrderService.Service`; `SupplyLine.SupplyItem`;
  `Invoice.Order`; `User.Company`. Required ones are non-nullable and initialised with `null!`,
  optional ones are nullable. The domain never assigns them and no rule follows them.
- `Invoice.Issue(number, order, amount, now)` is a static factory returning `Result<Invoice>`. It
  reads the id and the status of the order and refuses a `Draft` (`invoice.order_is_draft`).
  `MarkPaid(now)` refuses an invoice that is already paid (`invoice.already_paid`) and requires
  `now >= IssuedAt`. An empty number and a negative amount are contract violations.

### `Order` — public API

Every method takes `DateTimeOffset now` as its last parameter. When it succeeds it sets
`UpdatedAt = now` and increments `Version`; a draft starts at version 1. `StartDraft` is a static
factory returning the `Order`; `AddShipment` and `RecordOperation` return `Result<T>`; the others
return `Result`. A `shipmentId` the order does not have returns `shipment.not_found`.

Every method checks in the same order: the calling contract (`ArgumentException`, `now >= UpdatedAt`
included), the status of the order, lookups (`….not_found`), then its own refusals. So a wrong
argument on a completed order throws, and `DeclareCargo` with an unknown shipment on a submitted
order returns `order.not_draft`, not `shipment.not_found`.

**Draft** — in any other status: `order.not_draft`

| Method | Effect and refusals |
|---|---|
| `StartDraft(draftNumber, type, crossDockKind, createdBy, hubId)` | Creates a `Draft`; sets `CreatedAt`. Takes the `User` and reads both `CreatedById` and `CompanyId` from it, so the two cannot disagree. `crossDockKind` is required for `CrossDock` and must be `null` for `Consolidation` — otherwise `ArgumentException` |
| `AddShipment(refNumber)` → `Result<Shipment>` | Appends a shipment with the next `Position` (from 1). Refuses a Ref N the order already has (`shipment.duplicate_ref_number`) and a second shipment on a `CrossDock` (`shipment.cross_dock_has_one`) |
| `DeclareCargo(shipmentId, unitType, quantity)` | Adds a Cargo Line. Refuses a Unit Type the shipment already declares (`cargo_line.already_declared`) |
| `Schedule(scheduledAt)` | Sets `ScheduledAt` |
| `SetDestination(city, region)` | Sets the destination; both values are required |
| `ArrangeCarrier(carrierId)` · `ArrangeOwnDriver(driverName)` · `ArrangeSelfPickup()` | Sets `TransportKind` and exactly the field that kind needs; clears the other |
| `AddService(serviceId)` | Refuses a service the order already has (`service.already_added`) |
| `Submit(orderNumber)` | Requires `ScheduledAt`, a destination, a transport arrangement, exactly one shipment for `CrossDock` or at least two for `Consolidation`, and at least one Cargo Line on every shipment — otherwise `order.not_ready_to_submit`. Sets `Number`, `Status = Ready`, `SubmittedAt` |

**Hub work** — allowed in `Ready` and `InProgress`; otherwise `order.not_active`. Each of these
starts a `Ready` order: `Status = InProgress`, `StartedAt = now`.

| Method | Effect and refusals |
|---|---|
| `RecordArrival(shipmentId, trailerType, truckNumber, trailerNumber)` | Sets the inbound vehicle of the shipment and `ArrivedAt = now`. All arguments are required. Refuses a shipment that has already arrived (`shipment.already_arrived`): later trucks of the same shipment show in the trailer numbers of their Unloadings |
| `AssignDock(shipmentId, dock, bay, floorLead)` | Sets the dock of the shipment and `DockAssignedAt = now`. All arguments are required. A second call replaces the assignment: a dock can change |
| `RecordOperation(shipmentId, newOperation)` → `Result<Operation>` | See below |
| `FinishReceiving(shipmentId)` | Sets `ReceivedAt = now` and re-evaluates the Quantity delta. A shipment that never arrived is finished with no Unloading, which reports it as short. Refuses a shipment that is already received (`shipment.already_received`) |
| `SetWarehouseNote(shipmentId, note)` | Sets `WarehouseNote`, replacing the previous one. The note is required; clearing it is not supported |

**Closing** — these do not start the order

| Method | Effect and refusals |
|---|---|
| `ResolveAlert(alertId)` | Allowed in `Ready` and `InProgress`; otherwise `order.not_active`. Sets `ResolvedAt`. Refuses an unknown (`alert.not_found`) or resolved (`alert.already_resolved`) Alert |
| `Complete()` | Allowed in `InProgress`; in any other status `order.not_in_progress`. Then refuses, checked in this order: a shipment that is not received (`order.receiving_not_finished`), anything On stock (`order.stock_remaining`), an open Alert (`order.open_alerts`). Sets `Status = Completed`, `CompletedAt` |

**Any status but `Completed`** — otherwise `order.already_completed`. These do not start the order.

| Method | Effect and refusals |
|---|---|
| `TransferTo(transferHubId)` | Sets `TransferHubId`. Refuses the order's own hub (`order.same_hub`). Not draft-only: on a submitted order the transfer is arranged by platform staff |
| `AddSupply(supplyItem, quantity)` | Adds a Supply line with the id of the item and `UnitPrice` copied from it. The same item may be added again: each call is its own line with its own price |

`NewOperation` is a record: `Type`, `UnitType`, `Quantity`, `TrailerNumber?`, `IsBillable`,
`PhotoCount`, `CommentCount`. `TrailerNumber` is required for `Unloading` and `Loading` — they move
freight between a trailer and the dock — and must be absent for `Disposal` and `Restack`. Either
mismatch, a negative counter and a quantity below one are contract violations. The record is plain
data; `RecordOperation` checks it as part of its calling contract.

**`RecordOperation`**, in this order:

1. Refuse if the order is not `Ready` or `InProgress`, or the shipment is unknown.
2. For `Disposal`, `Restack` and `Loading`, refuse a quantity larger than what is On stock for that
   shipment and Unit Type (`operation.exceeds_stock`). `Unloading` has no upper bound: receiving
   more than declared is exactly what a positive Quantity delta records. `Restack` is checked
   against On stock and does not change it.
3. For an `Unloading`, take the known deltas of the shipment as they are now.
4. Add the Operation with `AppliedAt = now`. Start the order if it is `Ready`.
5. For an `Unloading`, take the known deltas again and apply the Quantity delta rule below. An
   `Unloading` is allowed after the shipment is received — the rest of a short shipment arriving
   late.
6. If the type requires a photo and `PhotoCount` is zero, and the shipment has no open
   `MissingPhoto` Alert — raise one. The types that require a photo are `Unloading` and `Disposal`,
   held in one named constant. Photos cannot be attached in this application, so this Alert ends
   only through `ResolveAlert`.

**Quantity delta of a shipment** — evaluated around an `Unloading` and around `FinishReceiving`,
nowhere else:

- The *known delta* of a Unit Type is actual minus declared when actual exceeds declared (a type
  that was not declared counts as declared zero), or when the shipment is received; otherwise it is
  zero.
- The aggregate compares the known deltas of all Unit Types before the change with those after it.
- After the change no delta is left → resolve the open `QuantityDelta` Alert, if there is one.
- After the change a delta is left, it differs from the one before, and no `QuantityDelta` Alert is
  open → raise one.
- Otherwise nothing happens. That is what keeps a manually resolved Alert resolved: finishing
  receiving with the same excess, an exact Unloading of another Unit Type and any `Loading` leave
  the delta as it was.

**Computed, never stored**

| Member | Meaning |
|---|---|
| `Shipment.DeclaredQuantity(unitType)` | Sum of Cargo Lines |
| `Shipment.ActualQuantity(unitType)` | Sum of `Unloading` |
| `Shipment.OnStock(unitType)` | Unloaded − disposed − loaded |
| `Shipment.HasQuantityDelta` | Any Unit Type has a known delta |
| `Order.HasOpenAlerts` | Any shipment has an Alert without `ResolvedAt` |
| `SupplyLine.LineTotal`, `Order.SupplySubtotal` | `Quantity × UnitPrice`, and their sum |

These work on a loaded aggregate and serve its rules. A read projection must not use them: EF Core
evaluates them on an entity without its children and returns `false` and `0` without an error. The
screens compute the same values in SQL.

### EF Core

- `UseSnakeCaseNamingConvention()`; configurations applied with `ApplyConfigurationsFromAssembly`.
- Every key is `ValueGeneratedNever()`. Without it EF Core treats a new child that already has a key
  as existing and issues an `UPDATE` instead of an `INSERT` when it is added to a loaded order.
- Collections are mapped to their backing fields. No lazy loading.
- `Version` is an ordinary column in this step. Whether it becomes the concurrency token is decided
  in step 8.

### Migrations and startup

- One migration, `InitialSchema`, creates everything above. A migration that has reached `main` is
  never edited; a change is a new migration.
- Created with
  `dotnet ef migrations add <Name> --project src/Freitty.Infrastructure --startup-project src/Freitty.Api --output-dir Persistence/Migrations`.
- `Program.cs` applies pending migrations after the host is built and before it starts listening,
  through one extension in Infrastructure that takes `app.Lifetime.ApplicationStopping` as its
  cancellation token. A failure stops the process: an instance that cannot bring its schema up to
  date must not serve requests.
- `/health/ready` keeps its meaning — the database is reachable.

### Also touched

- `.editorconfig` — `src/Freitty.Infrastructure/Persistence/Migrations/*.cs` is marked
  `generated_code = true`. Without it the scaffolded code fails the build and `dotnet format`.
- `tests/Freitty.Api.IntegrationTests/Health/HealthEndpointsTests.cs` — the two tests that start the
  API against an unreachable database are replaced (see *Tests*): with a migration at startup such
  an instance no longer starts.
- `README.md` — readiness wording (the API migrates the database at startup and exits if it
  cannot); a "Database migrations" section with the tool restore and the command above.
- `docs/er-diagram.md` — already written in grooming; correct it if the migration differs.

## Acceptance criteria

- [ ] `dotnet build Freitty.slnx -c Release` finishes with 0 warnings and 0 errors, and
      `dotnet format Freitty.slnx --verify-no-changes` exits 0.
- [ ] `Freitty.Domain` has no package or project reference.
- [ ] `docker compose down -v`, then `docker compose up -d --build --wait` exits 0; `\dt` in
      `psql` lists the 14 tables of *Schema* and `__ef_migrations_history`, all in `snake_case`.
- [ ] After `docker compose restart api` the API is ready again and its log says that no migration
      was applied.
- [ ] Every public member of an entity that changes state belongs to `Order`, `Invoice` or a
      constructor of reference data; no child of `Order` can be created or changed from outside the
      domain assembly.
- [ ] No domain method assigns a navigation to another aggregate or to reference data.
- [ ] No domain type reads a clock except through `Guid.CreateVersion7()`.
- [ ] `dotnet test` is green with Docker running and the compose stack stopped.
- [ ] Both CI jobs are green on the pushed commit.
- [ ] `docs/er-diagram.md` matches the migration.

## Tests

**Unit — `Freitty.Domain.Tests`** (instants are literals; no database, no fake clock)

*Draft and submission*

- A draft starts in `Draft` at version 1 with `CreatedAt` and `UpdatedAt` set, and takes its company
  from the user who created it.
- `CrossDock` without a kind and `Consolidation` with a kind are rejected as a contract violation.
- Shipments get consecutive positions; a second shipment on a `CrossDock`, a repeated Ref N, a
  repeated Unit Type on one shipment and a repeated service are refused.
- Each draft-only method is refused once the order is submitted.
- `Submit` is refused for each missing part — schedule, destination, transport, shipments, cargo
  on a shipment, a `Consolidation` with one shipment — and the message lists all of them.
- `Submit` sets the official number, `Ready` and `SubmittedAt`.
- Arranging another kind of transport clears the fields of the previous one.
- `TransferTo` works on a submitted order, refuses the order's own hub and is refused on a
  `Completed` order.

*Hub work and the ledger*

- Arrival, dock assignment, an operation, finishing receiving and a warehouse note each start a
  `Ready` order, and each is refused on a `Draft` and on a `Completed` order.
- A second arrival on one shipment is refused; a second dock assignment replaces the first.
- An `Unloading` or `Loading` without a trailer number, and a `Disposal` or `Restack` with one, are
  rejected as a contract violation.
- `Disposal`, `Restack` and `Loading` beyond what is On stock are refused, and the refused call
  leaves the order unchanged, `UpdatedAt` and `Version` included.
- On stock and Actual quantity are counted per shipment and per Unit Type; a `Restack` leaves On
  stock unchanged.
- An instant given with a non-zero offset and sub-microsecond ticks is stored with offset zero and
  whole microseconds; truck and trailer numbers are stored trimmed and upper-cased.
- An instant earlier than `UpdatedAt` is rejected as a contract violation.

*Quantity delta*

- An `Unloading` below declared raises no `QuantityDelta` Alert while the shipment is not received:
  a split unload and a shipment with two Unit Types unloaded one after the other stay quiet.
- An `Unloading` above declared raises a `QuantityDelta` Alert at once; so does a Unit Type that was
  not declared.
- `FinishReceiving` raises the `QuantityDelta` Alert for a short shipment and for one with no
  Unloading at all, and raises none when the quantities match; finishing twice is refused.
- After receiving is finished, an `Unloading` that evens the quantities resolves the Alert; one that
  does not leaves a single open Alert.
- A Quantity delta on one shipment raises no Alert on another.
- After an excess Alert is resolved by hand, `FinishReceiving`, a `Loading` and an exact `Unloading`
  of another Unit Type raise no `QuantityDelta` Alert; an `Unloading` that changes the delta raises
  a new one.

*Other alerts and completion*

- `Unloading` and `Disposal` without a photo raise one `MissingPhoto` Alert per shipment;
  with a photo, or for `Restack` and `Loading`, none.
- `ResolveAlert` sets `ResolvedAt`; an unknown or resolved Alert is refused, and so is the call on
  an order that is not active.
- `Complete` is refused when the order is not in progress, when a shipment is not received, when
  something is On stock and when an Alert is open — in that order when several apply; otherwise it
  sets `Completed` and `CompletedAt`.
- An order whose only shipment never arrived can be finished, its Alert resolved, and completed.

*Supplies, reference data, invoices*

- A Supply line copies the item's price; `LineTotal` and `SupplySubtotal` add up; the same item can
  be added twice; a supply is refused on a `Completed` order.
- Every successful change sets `UpdatedAt` to its instant and adds one to `Version`.
- Each refusal carries its code and its kind: the two `not_found` refusals are `NotFound`, the rest
  `Conflict`.
- `User.Initials` for one and two words; a `Hub` with an unknown or a Windows time zone id is
  rejected.
- An invoice is refused for a `Draft` order; paying twice is refused. Its instants are stored with
  offset zero; an empty number and a negative amount are rejected as contract violations.

**Integration — `Freitty.Api.IntegrationTests`**

- An empty database is migrated when the API starts.
- The model has no pending changes against the migration snapshot.
- An order built through the aggregate — shipments, cargo, operations, alerts, Supply lines,
  services — is saved and read back in a new context with every value intact.
- A service added to a draft loaded from the database, and an Operation and a Supply line added to
  a submitted order loaded from the database, are inserted; no reference data row is inserted with
  them.
- The database refuses a second open Alert of one type on a shipment, and accepts it once the first
  is resolved.
- The database refuses: a duplicate order number within a company; the same official number in two
  companies, while two companies may hold the same draft number; a Cargo Line with quantity zero; a
  carrier on an order that has no transport kind; a `Completed` order without `completed_at`; a
  status that is not a member of the enum.
- Deleting a draft removes its shipments and Cargo Lines; deleting an order that has an Operation
  is refused by the database.
- Readiness returns `503` and liveness `200` after the database goes away: the test starts the API
  on a database created for it, drops that database `WITH (FORCE)` and then asks.

How these are written:

- `PostgresFixture` gains a method that creates an empty database in the shared container and
  returns its connection string, for the tests that need their own.
- There is no endpoint yet, so these tests take `AppDbContext` from the services of `ApiFactory`
  instead of calling HTTP as [ADR 0002](../adr/0002-integration-tests-on-real-postgres.md)
  describes. That returns with the first endpoint.
- A test of a database constraint writes the offending row with raw SQL — the aggregate would
  refuse it first — through `ExecuteSqlAsync` with an interpolated string, and asserts the
  `SqlState` and the `ConstraintName` of the `PostgresException`, not merely that something was
  thrown. A delete is issued with `ExecuteDeleteAsync`, so the foreign key answers and not the
  change tracker.
- A contract violation is asserted with `Assert.ThrowsAny<ArgumentException>`: the guard helpers
  throw subclasses. The UTC test asserts `Offset == TimeSpan.Zero`: `DateTimeOffset` equality
  ignores the offset.
- Test classes run in parallel on one database, and names, SKUs and order numbers are unique. Every
  test creates its own reference data with unique values.

## Out of scope

- `IAppDbContext`, handlers, endpoints, DTOs, the current company and user — [step 4](04-order-list.md).
- Seed data and `TimeProvider` registration — [step 3](03-seed.md).
- Search and filter indexes — step 4.
- The concurrency token, the transaction around `RecordOperation` and protection against a repeated
  request — [step 8](08-write-flow.md).
- Order number generation and the create / submit endpoints — not in the assignment. When they come:
  a lookup by number is always scoped by company, and a draft number is never issued twice, or an
  old link opens another order.
- Amending a submitted order: changing cargo, removing a shipment that never arrived. Correcting an
  Operation — see [ADR 0005](../adr/0005-stored-state-not-event-sourcing.md).
- Cancelling an order, reopening a completed one, putting one on hold.
- Deleting an order. The product deletes drafts only; nothing in the domain deletes.
- A `Restack` that changes the pallet count or the Unit Type (eleven pallets rebuilt into nine).
- Attaching a photo, which is what would resolve a `MissingPhoto` Alert automatically.
- Who recorded an Operation, who resolved an Alert and why — needs authentication.
- Invoice lines, an invoice over several orders, top-ups, payments, a second currency.
- The seven pipeline columns (`Consolidated`, `In Transit`, `Deconsolidated`) and Operations at the
  transfer hub. This is more than new members of `OrderStatus`: the ledger has no hub, so an
  Unloading at the second hub would count as received twice. It needs `operations.hub_id`, Actual
  quantity defined at the origin hub, and arrival and dock per hub.
- Presentation derived from the model — the "Alert" badge, "On Stock", "Next: …", the sub-order
  label `FR001676-1`, "awaiting your action" — is defined in steps 4–6 from columns that exist.
- Not reproduced, because nothing in the model can produce them: the countdown in "Next: Loading ·
  2h 14m" (it needs a planned pickup instant on the order), the denominators of the photo and
  comment counters, "via External PDF" / "Order with photos" beside the destination, and the
  neighbouring docks drawn on the dock card.
