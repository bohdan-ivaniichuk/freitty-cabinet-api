# Freitty Cabinet API

Backend for the Freitty client cabinet (Dashboard, Order List, Order Detail) — .NET 10, FastEndpoints, EF Core, PostgreSQL.

> 🚧 Work in progress. Test assignment — see [`assignment/`](assignment) for the task and [`docs/plan.md`](docs/plan.md) for the implementation plan.

## What is in the box

Three pages, served by the API — under Docker Compose, the Dashboard at http://localhost:8080/, the
Order List at http://localhost:8080/orders.html and an order at
http://localhost:8080/order.html?n=FR001383 — the API reference at http://localhost:8080/scalar/ and
the OpenAPI document at http://localhost:8080/openapi/v1.json. The endpoints:

| Method | Route | What |
|---|---|---|
| `GET` | `/api/v1/orders` | One page of order cards, with the tabs, filters and search of the Order List |
| `GET` | `/api/v1/orders/counts` | The counter of each Order List tab, under the same filters |
| `GET` | `/api/v1/orders/export` | The whole filtered Order List as a CSV file |
| `GET` | `/api/v1/orders/{number}` | The detail of an order |
| `POST` | `/api/v1/orders/{number}/operations` | Records an operation on a shipment |
| `GET` | `/api/v1/dashboard` | The tiles, the first active orders and the activity chart |
| `GET` | `/api/v1/hubs` | The options of the Hub filter |
| `GET` | `/api/v1/me` | The current user, the company and the number on the Orders badge |

## Run locally

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Docker Compose — enough to run the app.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — only for `dotnet run` and
  `dotnet test`.

### The whole stack in Docker

```bash
docker compose up -d --build --wait
```

This starts PostgreSQL and the API; host ports 5432 and 8080 must be free. When the command returns,
both are healthy:

| URL | What |
|---|---|
| http://localhost:8080/ | The cabinet: the Dashboard, the order list (`/orders.html`) and an order (`/order.html?n=FR001383`) |
| http://localhost:8080/health/live | Liveness — `200` while the process serves requests |
| http://localhost:8080/health/ready | Readiness — `200` when the API reaches PostgreSQL, `503` otherwise |
| http://localhost:8080/scalar | The API reference (Scalar), with requests that can be sent from the page |
| http://localhost:8080/openapi/v1.json | The OpenAPI document; the endpoints are under `/api/v1` |

The API applies pending database migrations at startup, before it listens. If it cannot — the database is
unreachable or a migration fails — the process exits instead of serving requests. The database itself must
already exist (Compose creates it); instances starting together take turns to migrate it.

The API reads its reporting time zone — where "today" and "this week" begin — from `Cabinet__TimeZone`
(`America/Toronto` by default, in `appsettings.json`). It must be an IANA id; any other value stops the startup.

Stop with `docker compose down`; add `-v` to delete the database as well.

### The API from source

Start only the database, then run the API:

```bash
docker compose up -d --wait postgres
dotnet run --project src/Freitty.Api
```

The API listens on http://localhost:5080 (for example, http://localhost:5080/health/ready): the cabinet
at http://localhost:5080/ and the API reference at http://localhost:5080/scalar. In Development the SQL
that EF Core sends is logged.

### Sample data

When `Seed:Enabled` is `true` and the database is empty — it has no company — the API fills it with
sample data at startup, right after the migrations:

- the company R-way Transport with four users, the Markham and Toronto hubs, three carriers, two
  services and sixteen supply items;
- ten scripted orders in every status, two of them with open alerts, that the screens and the demo
  rely on;
- sixty completed orders over the last three months, for the dashboard figures and the chart;
- an invoice for every completed order, one of them unpaid.

The setting is on in `appsettings.Development.json` and in `docker-compose.yml`, and off everywhere
else. Every order is built through the domain model, so the data obeys the same rules as an order
made through the API.

Dates are relative to the day before the database was seeded. The Docker stack — the demo — keeps
them fresh: `Seed__Refresh` is `true` in `docker-compose.yml`, so the API rebuilds the sample data at
every start and again soon after midnight in Toronto, when that day changes (it checks every 15
minutes). A rebuild deletes everything in the database, operations recorded from an order page
included, and seeds again in one transaction; requests wait for it, well under a second. To keep
what you recorded, set `Seed__Refresh` to `"false"` in `docker-compose.yml`, or remove it.

Without `Seed:Refresh` — as with `dotnet run`, where `appsettings.Development.json` turns on only
`Seed:Enabled` — the data ages: a database seeded a week ago shows a week-old picture, and a database
that already has a company is left as it is. To start over, empty the database:

```bash
docker compose down -v
docker compose up -d --build --wait
```

or, running the API from source:

```bash
docker compose down -v
docker compose up -d --wait postgres
dotnet run --project src/Freitty.Api
```

### Database migrations

Migrations live in `src/Freitty.Infrastructure/Persistence/Migrations` and are applied by the API at startup.
To add one, restore the local tools once, then run:

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/Freitty.Infrastructure --startup-project src/Freitty.Api --output-dir Persistence/Migrations
```

A migration that has reached `main` is never edited; a change is a new migration.

The first start against an empty database logs one `Failed executing DbCommand` at `ERR`: EF Core
reads its migrations history table before it has created it. The migrations follow on the next lines.

### Tests

```bash
dotnet test
```

Integration tests start their own PostgreSQL container through Testcontainers, so Docker must be
running; the compose stack does not need to be.
