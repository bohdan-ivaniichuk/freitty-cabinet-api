# Freitty Cabinet API

Backend for the Freitty client cabinet (Dashboard, Order List, Order Detail) — .NET 10, FastEndpoints, EF Core, PostgreSQL.

A test assignment: three screens of a finished mockup, fed by a backend — deployed, with the
decisions written down.

## See it running

Everything below opens on the deployed demo. Its sample data is rebuilt every night and at every
release, so anything you record there is yours to break.

| Open | What it shows |
|---|---|
| [Dashboard](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/) | The tiles, the first active orders, the activity chart with its four periods |
| [Order List](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/orders.html) | Tabs with counters, filters, search, pages — as [cards](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/orders.html), a [table](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/orders.html?view=table) or a [pipeline board](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/orders.html?view=pipeline), where a draft is submitted by dragging it to New |
| [Order FR001383](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/order.html?n=FR001383) | An order in progress with a quantity delta: record an operation with **+ Operation**, open the photos with 📷 |
| [Order FR001674](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/order.html?n=FR001674) | An order with a missing photo: uploading one to its Unloading closes the alert |
| [API reference](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/scalar/) | Every endpoint with its request, responses and errors; requests can be sent from the page |
| [OpenAPI document](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/openapi/v1.json) | The same contract as JSON |

Where the decisions are:

| Read | What it is |
|---|---|
| [`docs/plan.md`](docs/plan.md) | The plan made before the first line of code, and the order of work |
| [`CONTEXT.md`](CONTEXT.md) | The vocabulary: what an Order, a Shipment, an Operation and an Alert are |
| [`docs/er-diagram.md`](docs/er-diagram.md) | The schema and what references what |
| [`docs/adr/`](docs/adr) | Seventeen decisions, each with its alternatives and what it costs |
| [`docs/steps/`](docs/steps) | The spec of every step, written before its code |
| [`docs/explain/`](docs/explain) | Query plans before and after each index, on a volume data set |
| [`docs/mockup-analysis.md`](docs/mockup-analysis.md) | Where the mockup contradicts itself and how the model settles it |
| [`docs/engineering/code-rules.md`](docs/engineering/code-rules.md) | The rules every C# file follows |
| [`docs/deployment.md`](docs/deployment.md) | The runbook: provisioning, operations, teardown |

## The API

All under `/api/v1`; a `GET` below opens on the demo, the writes are best tried from the
[API reference](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/scalar/).

| Method | Route | What |
|---|---|---|
| `GET` | [`/api/v1/orders`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/orders) | One page of order cards, with the tabs, filters and search of the Order List |
| `GET` | [`/api/v1/orders/counts`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/orders/counts) | The counter of each Order List tab, under the same filters |
| `GET` | [`/api/v1/orders/export`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/orders/export) | The whole filtered Order List as a CSV file |
| `GET` | [`/api/v1/orders/pipeline`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/orders/pipeline) | The Order List as a board: every status with its total and first cards |
| `GET` | [`/api/v1/orders/{number}`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/orders/FR001383) | The detail of an order |
| `POST` | `/api/v1/orders/{number}/operations` | Records an operation on a shipment |
| `POST` | `/api/v1/orders/{number}/submit` | Submits a draft: Ready, under the next official number |
| `GET` | [`/api/v1/orders/{number}/photos`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/orders/FR001383/photos) | Every photo of an order, in the order of its page |
| `GET` | `/api/v1/orders/{number}/photos/{photoId}` | The image of a photo, streamed from blob storage |
| `POST` | `/api/v1/orders/{number}/operations/{operationId}/photos` | Uploads one to five photos of an operation (multipart) |
| `GET` | [`/api/v1/dashboard`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/dashboard) | The tiles, the first active orders and the activity chart |
| `GET` | [`/api/v1/hubs`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/hubs) | The options of the Hub filter |
| `GET` | [`/api/v1/me`](https://freitty-cabinet.purplemoss-8046cc4d.northeurope.azurecontainerapps.io/api/v1/me) | The current user, the company and the number on the Orders badge |

The demo is temporary. Once it is taken down these links stop working, and
`docker compose up` below gives the same application on `localhost:8080`.

## Run locally

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Docker Compose — enough to run the app.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — only for `dotnet run` and
  `dotnet test`.

### The whole stack in Docker

```bash
docker compose up -d --build --wait
```

This starts PostgreSQL, Azurite — the Azure Blob Storage emulator, which keeps the photos — and the
API; host ports 5432, 10000 and 8080 must be free. When the command returns, all three are healthy:

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

Photos are blobs of the private container `photos`, which the API creates at startup and serves
itself ([ADR 0017](docs/adr/0017-photos-in-blob-storage-behind-the-api.md)). The API reads the
storage's connection string from `ConnectionStrings__PhotoStorage` and does not start without it;
Compose points it at Azurite with the emulator's well-known development account, published on
loopback only. `Photos__UploadsEnabled` set to `false` switches uploads off (`503`); stored photos
are still served.

The API reads its reporting time zone — where "today" and "this week" begin — from `Cabinet__TimeZone`
(`America/Toronto` by default, in `appsettings.json`). It must be an IANA id; any other value stops the startup.

Stop with `docker compose down`; add `-v` to delete the database and the stored photos as well.

### The API from source

Start only the database and the photo storage, then run the API:

```bash
docker compose up -d --wait postgres azurite
dotnet run --project src/Freitty.Api
```

`appsettings.Development.json` points the API at both: PostgreSQL on port 5432 and Azurite on
10000 (`UseDevelopmentStorage=true`).

The API listens on http://localhost:5080 (for example, http://localhost:5080/health/ready): the cabinet
at http://localhost:5080/ and the API reference at http://localhost:5080/scalar. In Development the SQL
that EF Core sends is logged.

### Sample data

When `Seed:Enabled` is `true` and the database is empty — it has no company — the API fills it with
sample data at startup, right after the migrations:

- the company R-way Transport with four users, the Markham and Toronto hubs, three carriers, two
  services and sixteen supply items;
- eleven scripted orders in every status, two of them with open alerts, that the screens and the
  demo rely on — among them two drafts: DRAFT-003, which lacks what a submit needs, and DRAFT-004,
  which can be submitted from the Pipeline layout of the Order List;
- sixty completed orders over the last three months, for the dashboard figures and the chart;
- an invoice for every completed order, one of them unpaid;
- a photo for every photo an operation counts: each is a row that points at one of four sample
  images — flat placeholder pictures drawn by
  `src/Freitty.Infrastructure/Persistence/Seed/Images/draw_sample_photos.py`, not photographs —
  which the API uploads to the photo storage whenever it seeds.

The setting is on in `appsettings.Development.json` and in `docker-compose.yml`, and off everywhere
else. Every order is built through the domain model, so the data obeys the same rules as an order
made through the API.

Dates are relative to the day before the database was seeded. The Docker stack — the demo — keeps
them fresh: `Seed__Refresh` is `true` in `docker-compose.yml`, so the API rebuilds the sample data at
every start and again soon after midnight in Toronto, when that day changes (it checks every 15
minutes). A rebuild deletes everything in the database, operations recorded from an order page
included, and seeds again in one transaction; requests wait for it, well under a second. Photos
uploaded from an order page are deleted with it, one blob at a time, which adds a little to that
wait; the sample images stay. To keep
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
docker compose up -d --wait postgres azurite
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

Integration tests start their own PostgreSQL and Azurite containers through Testcontainers, so
Docker must be running; the compose stack does not need to be.

## Deployment

The demo runs on Azure, in one resource group in North Europe
([ADR 0014](docs/adr/0014-azure-container-apps-and-managed-postgresql.md)):

- one container app on Azure Container Apps — exactly one replica, always on, 0.25 vCPU and
  0.5 GiB, behind HTTPS ingress; its container, probes and scale are in
  [`deploy/containerapp.yaml`](deploy/containerapp.yaml);
- an Azure Database for PostgreSQL flexible server, PostgreSQL 18, which the app reaches over
  verified TLS;
- an Azure Container Registry that holds the images, pulled by the app with a managed identity;
- a storage account with one private container for the photos, which only the API reads;
- a Log Analytics workspace for the logs.

A release is a push to `main`. CI runs the tests and the Compose stack; the image the stack job
tested is pushed to the registry and, when both jobs pass, deployed by its digest — nothing is
rebuilt.
The new revision starts beside the running one, and the traffic moves to it only once it passes
its probes; the workflow then checks the public URL. A version that cannot start never takes
traffic: the old one keeps serving and the run is red. GitHub signs in to Azure with OIDC, so no
cloud credential is stored in the repository.

The new revision migrates the database while the old one still serves, so every migration must
work with the running version: additive only — new tables, nullable columns or columns with
defaults, new indexes. A rename or a drop takes two releases: stop using it, then remove it.

The sample data is rebuilt at every start — so at every deploy and restart — and again soon after
midnight in Toronto, as under Compose.

To roll back, list the revisions with their images and deploy an earlier image:

```bash
az containerapp revision list --resource-group rg-freitty-demo --name freitty-cabinet --all \
  --query "[].{name:name, created:properties.createdTime, image:properties.template.containers[0].image}" --output table
az containerapp update --resource-group rg-freitty-demo --name freitty-cabinet \
  --image "<image@sha256:… of the chosen revision>" --revision-suffix "rollback-$(date +%s)" --output none
```

This is safe when no migration was added between the two versions; across a migration, only if
the old version can still start on the new schema. Otherwise its revision never becomes ready,
the current one keeps serving, and the fix is a forward commit. The next push to `main` deploys
`main` again, so a lasting rollback is a `git revert` pushed to `main`.

To tear down, delete the GitHub repository variables first — CI then stops pushing and deploying
and stays green — and then the resource group, which deletes everything billable:

```bash
az group delete --name rg-freitty-demo --yes
```

The provisioning runbook, day-to-day operations and the full teardown are in
[`docs/deployment.md`](docs/deployment.md).
