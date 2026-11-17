# Freitty Cabinet API

Backend for the Freitty client cabinet (Dashboard, Order List, Order Detail) — .NET 10, FastEndpoints, EF Core, PostgreSQL.

> 🚧 Work in progress. Test assignment — see [`assignment/`](assignment) for the task and [`docs/plan.md`](docs/plan.md) for the implementation plan.

## Run locally

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Docker Compose — enough to run the app.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — only for `dotnet run` and
  `dotnet test`.

### The whole stack in Docker

```bash
docker compose up -d --build --wait
```

This starts PostgreSQL and the API. When the command returns, both are healthy:

| URL | What |
|---|---|
| http://localhost:8080/health/live | Liveness — `200` while the process serves requests |
| http://localhost:8080/health/ready | Readiness — `200` when the API reaches PostgreSQL, `503` otherwise |

The API applies pending database migrations at startup, before it listens. If it cannot — the database is
unreachable or a migration fails — the process exits instead of serving requests.

Stop with `docker compose down`; add `-v` to delete the database as well.

### The API from source

Start only the database, then run the API:

```bash
docker compose up -d --wait postgres
dotnet run --project src/Freitty.Api
```

The API listens on http://localhost:5080 (for example, http://localhost:5080/health/ready).

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

Dates are relative to the day before the database was seeded, so the data ages: a database seeded a
week ago shows a week-old picture. The API never deletes or refreshes sample data; a database that
already has a company is left as it is. To start over, empty the database:

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

### Tests

```bash
dotnet test
```

Integration tests start their own PostgreSQL container through Testcontainers, so Docker must be
running; the compose stack does not need to be.
