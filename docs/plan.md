# Implementation plan

Decisions agreed before implementation. Source of truth for the agent and for the presentation. Inputs live in [`/assignment`](../assignment): task, original mockup, mockup analysis.

## 1. Goal

Backend (.NET 10 + PostgreSQL) that feeds the three mockup screens — **Dashboard**, **Order List**, **Order Detail** — through an API, with the existing HTML wired to it. Deployed, with README and a short presentation. Evaluated by the deployed app + presentation.

## 2. What the role values → what to show

The vacancy (Backend .NET, logistics/freight, cross-docking platform) stresses these. Each must be visible in the result:

| Role emphasis | How it shows up here |
|---|---|
| PostgreSQL: indexes, query plans, transactions, isolation levels | `pg_trgm` GIN index for search, composite indexes for list filters, `EXPLAIN ANALYZE` before/after in `docs/`, one write flow in a transaction with optimistic concurrency |
| EF Core: change tracking, N+1, eager loading, query optimization | `AsNoTracking` + projections in read handlers, `AsSplitQuery` on the detail query, no lazy loading |
| API: contracts, versioning, error handling | `/api/v1/...`, `ProblemDetails` everywhere, OpenAPI + Scalar |
| Git · Docker · CI/CD — "can build and deploy myself" | Dockerfile, docker-compose for the whole local stack (API + Postgres), GitHub Actions: build → test → image → deploy |
| Tests: unit for domain, integration for critical paths | xUnit; domain unit tests; integration tests on filters/search/pagination with Testcontainers (real Postgres) |
| AI tools as part of the workflow | `CLAUDE.md`, hooks/skills in repo; one presentation slide on the workflow |
| Bonus: Dapper, OpenAPI/Swagger, xUnit, logistics domain | Dapper for dashboard analytics SQL; Scalar; logistics-aware domain model |
| Small team, fast decisions | Pragmatic: every library justified, no ceremony for its own sake |

## 3. Stack

- **.NET 10**, C#
- **FastEndpoints** (REPR). Presentation point: application layer is transport-agnostic and could be exposed via Minimal API just as well.
- **FastEndpoints command bus** with a pipeline: logging → **FluentValidation** → handler (no MediatR — commercial since 2025)
- **Mapperly** (source-generated mapping; no AutoMapper — commercial). Read-side projections stay in `Select` / `IQueryable` so SQL does the work
- **EF Core + Npgsql** for the domain and writes; **Dapper** for dashboard aggregates
- **PostgreSQL**: migrations, seed on startup, `pg_trgm`, composite indexes, optimistic concurrency token
- **ProblemDetails**, `/api/v1`, **Scalar** (OpenAPI UI)
- **Serilog**
- **xUnit + Testcontainers**
- **Docker**, **GitHub Actions**, deploy to Render / Railway / Fly.io (check current free-tier terms before choosing; Neon is an option for Postgres)

## 4. Architecture

Light Clean Architecture:

```
src/
├── Freitty.Domain/          # entities, enums, domain rules (status machine, delta, alerts)
├── Freitty.Application/     # queries/commands + handlers, DTOs, validators, IAppDbContext
├── Freitty.Infrastructure/  # EF Core DbContext, configurations, migrations, seed, Dapper queries
└── Freitty.Api/             # FastEndpoints, ProblemDetails, OpenAPI, wwwroot (frontend)
tests/
├── Freitty.Domain.Tests/
└── Freitty.Api.IntegrationTests/   # Testcontainers Postgres
```

Rules:
- CQRS here is mostly **Q**: read handlers project straight to DTOs (`AsNoTracking`, `Select`). Entities are never loaded just to be mapped.
- No generic repository over EF — `DbContext` already is Unit of Work + Repository; Application depends on `IAppDbContext`.
- Domain does not depend on infrastructure.
- Deliberately skipped (say so in the presentation): MediatR, AutoMapper, generic repositories, value objects for every field.

## 5. Domain model

Settled in the grooming of step 2: [`steps/02-domain.md`](steps/02-domain.md) is the contract,
[`er-diagram.md`](er-diagram.md) the picture, [`CONTEXT.md`](../CONTEXT.md) the vocabulary.

| Entity | Key fields |
|---|---|
| **Company** | Name, Balance — the client that owns the cabinet; the hub's screens call it the customer |
| **User** | Name, Initials (derived), Role (Admin / Dispatcher), CompanyId |
| **Hub** | Name (Markham, Toronto), ShortName, Region, TimeZone |
| **Carrier** | Name, Phone — real carriers only |
| **Order** | Number (`DRAFT-003` → `FR001676`), Type (CrossDock / Consolidation), CrossDockKind (Storage / Pickup), Status, CompanyId, CreatedById, HubId, TransferHubId, ScheduledAt, destination city and region, TransportKind (Carrier / OwnDriver / SelfPickup) with CarrierId or DriverName, milestone instants, UpdatedAt, Version |
| **Shipment** | OrderId, Position (`-1`), RefNumber, WarehouseNote, arrival (trailer type, truck, trailer, ArrivedAt), ReceivedAt, dock assignment |
| **CargoLine** | ShipmentId, UnitType (Standard 48×40 / XL), Quantity — the declared quantity |
| **Operation** | ShipmentId, Type (Unloading / Disposal / Restack / Loading), UnitType, Quantity, TrailerNumber, AppliedAt, IsBillable, PhotoCount, CommentCount — the ledger of actual quantities |
| **Alert** | ShipmentId, Type (QuantityDelta / MissingPhoto), RaisedAt, ResolvedAt |
| **Service** (M:N) | Transload, Restock & Rework |
| **SupplyItem** + **SupplyLine** | SKU, Category, UnitPrice; SupplyLine keeps Quantity + **UnitPrice snapshot** |
| **Invoice** | Number (`#001812`), OrderId, Amount, IssuedAt, PaidAt → spend chart |

Key decisions (presentation material):
- **`Order` is one aggregate; the rest is reference data** — [ADR 0003](adr/0003-order-is-one-aggregate.md).
- **Every quantity has one source.** Declared lives in cargo lines, actual in the operations ledger;
  delta, totals and "Need Attention" are computed, not stored →
  no drift — [ADR 0004](adr/0004-quantities-have-one-source.md).
- **An order is made of shipments.** Cross-Dock has one, Consolidation several → one model, no special
  cases; cargo, arrival, operations and alerts belong to the shipment.
- **Stored state with an append-only ledger, not event sourcing** —
  [ADR 0005](adr/0005-stored-state-not-event-sourcing.md).
- **One status enum** (Draft → Ready → InProgress → Completed) for the freight only; payment belongs
  to the invoice, and an alert is a stored record, not a status.
- **Supply price is snapshotted** on the order line → catalog changes don't rewrite history.
- **UUID v7 keys from the domain** — [ADR 0006](adr/0006-uuid-v7-keys-from-the-domain.md).

## 6. API

```
GET  /api/v1/dashboard?period=            KPIs + first active orders + activity series
GET  /api/v1/orders?type=&status=&alerts=&hub=&date=&q=&page=&pageSize=
GET  /api/v1/orders/counts?hub=&date=&status=&q=   tab counters, following the filters
GET  /api/v1/orders/{number}              detail: shipments, cargo, services, operations, supplies
POST /api/v1/orders/{number}/operations   the one write flow (see below)
GET  /api/v1/hubs                         options of the Hub filter
GET  /api/v1/me                           the shell: user, company balance, attention badge
```

- Filtering, search and pagination in SQL. Search: `ILIKE` on number and Ref N (further fields are decided in step 4), backed by `pg_trgm` GIN indexes. Pagination: offset/limit (mention keyset as the scale-up path).
- Dashboard KPIs and charts: Dapper + SQL (`date_trunc`, `FILTER (WHERE ...)`, CTEs).
- **Write flow** (not required by the task, added to show transactions/isolation): add an operation → in one transaction append it to the ledger and open/resolve the quantity delta alert; optimistic concurrency on the order row (`Order.Version` as the token, [ADR 0013](adr/0013-optimistic-concurrency-on-order-version.md)), 409 ProblemDetails on conflict.
- Validation (FluentValidation): page and pageSize bounds, search length, enum values, operation payload.

## 7. Frontend

Same HTML, kept 1:1:
- Cut the mockup into three pages in `src/Freitty.Api/wwwroot`: `index.html` (Dashboard), `orders.html` (Order List), `order.html?n=FR001383` (Detail). Drop the fake browser frame; keep `.fc-shell`. Shared styles → `app.css`, class names untouched.
- Hardcoded cards → `<template>` elements with the same markup; small vanilla JS (`fetch` + clone template + fill).
- Tabs, filters, search, Prev/Next change query params and refetch. Card click → detail page.
- Served by the same ASP.NET app (static files): one origin, no CORS, one deployable.
- No React/Blazor: the task says the markup is done; the evaluation is the backend.

## 8. Repo layout

```
assignment/   inputs, never modified
docs/         plan.md, adr/, er-diagram.md (mermaid), explain/ (query plans), presentation (Marp/Slidev)
src/          solution
tests/
.github/workflows/
CLAUDE.md, .claude/ (hooks, skills)
Dockerfile, docker-compose.yml, README.md
```

## 9. Presentation (10–15 min)

1. Live demo of the deployed app: dashboard, filters/search/pagination, detail, the write flow.
2. Domain model + ER diagram; key decisions (section 5).
3. Architecture: layers, pipeline, why FastEndpoints, "could be Minimal API".
4. Database: indexes + `EXPLAIN ANALYZE` before/after, transaction + concurrency.
5. CI/CD + tests.
6. AI workflow (CLAUDE.md, hooks, skills) — "I design and decide, the agent speeds up the routine".
7. Walk through 3–4 prepared files in the repo.

Mention that the mockup data was inconsistent and how it was normalized ([mockup-analysis.md](../assignment/mockup-analysis.md)).

## 10. Order of work

1. Solution skeleton, health checks, Dockerfile, Docker Compose (API + Postgres), CI build + test
2. Domain + EF configurations + migrations + domain unit tests
3. Seed (normalized mockup data)
4. Order List query (filters, search, pagination, counts) + indexes + integration tests; the first endpoint, so FastEndpoints, the pipeline, `ProblemDetails` and OpenAPI are wired here
5. Order Detail query
6. Dashboard (Dapper)
7. Frontend wiring (3 pages)
8. Write flow (transaction + concurrency)
9. Deploy + CD
10. Docs: README, ADRs, ER, EXPLAIN, presentation

If time is short, cut in this order: Dapper (do dashboard in EF), write flow. Do **not** cut CI/CD, query plans or tests — the role emphasizes them.
