# 01 — Skeleton

## Goal

A walking skeleton: the solution builds with strict checks, the API starts, reaches PostgreSQL and
reports it through health endpoints, and one command brings the whole stack up in Docker. CI builds,
tests and smoke-tests that stack on every push, so each later step only adds features.

## Decisions

**Scope**

- Walking skeleton, not empty scaffolding — Testcontainers in CI, the connection string path and the
  container image are proven now, not in the middle of step 4.
- Nothing is created before it has a purpose. No placeholder endpoint, no empty interface, no empty
  test project. What that defers is listed in *Out of scope*.
- FastEndpoints is **not** wired in this step. It refuses to start with zero endpoints, and an
  endpoint written only to satisfy it would be deleted in step 4. The API framework, the error
  contract and OpenAPI arrive together with the first real endpoint.

**Solution**

- Layout and dependency direction follow [ADR 0001](../adr/0001-light-clean-architecture.md).
  `Freitty.Domain` and `Freitty.Application` contain only a project file in this step.
- `Freitty.slnx` at the repository root; `global.json` pins SDK `10.0.100` with
  `rollForward: latestFeature` — local machines, CI and the Docker build use the same major line.
- `Directory.Build.props` holds everything shared: `net10.0`, `Nullable`, `ImplicitUsings`,
  `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`. Warnings are errors locally too, so a local
  build and a CI build cannot disagree. NuGet audit warnings (`NU1900`–`NU1904`) are the exception:
  they stay warnings, so an advisory published after a commit does not break restore, CI or the
  Docker build without any code change.
- Central Package Management (`Directory.Packages.props`) — one place for versions across all
  projects.
  Latest stable versions at implementation time, looked up, not recalled; no prereleases.
  The two `Microsoft.*` packages follow the `net10.0` runtime (10.0.x): their 11.x targets `net11.0` only.
- Packages in this step, nothing more:

  | Project | Packages |
  |---|---|
  | `Freitty.Infrastructure` | `Npgsql.EntityFrameworkCore.PostgreSQL` |
  | `Freitty.Api` | `Serilog.AspNetCore`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` |
  | `Freitty.Api.IntegrationTests` | `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql` |

**Host**

- `AppDbContext` (`Freitty.Infrastructure/Persistence`) is sealed and empty: no `DbSet`s, no
  migrations. It exists so the readiness check and the tests exercise the real connection path.
  Registered by an `AddInfrastructure(configuration)` extension in Infrastructure.
- The connection string is `ConnectionStrings:Postgres`. A missing value fails startup with a message
  that names the environment variable to set (`ConnectionStrings__Postgres`) — a wrong deployment
  should fail at boot, not on the first request.
- Health is ASP.NET Core health checks, not an API endpoint: it is an operational contract for the
  container runtime, outside `/api/v1` and outside OpenAPI. Liveness and readiness are separate so a
  database outage marks the instance not ready without making the platform restart it.
- Serilog: console sink only, configured from the `Serilog` section of `appsettings.json`, request
  logging on. Health requests are logged below the minimum level — a probe every five seconds must
  not bury real requests. `Microsoft.EntityFrameworkCore.Database.Command` is raised to `Warning` in
  `appsettings.json` for the same reason: the readiness check's `SELECT 1` would otherwise log on
  every probe. The step with the first query sets it back to `Information` in
  `appsettings.Development.json`, so `dotnet run` shows the generated SQL.
- No HTTPS in the app: TLS ends at the hosting platform. No `UseHttpsRedirection`.

**Docker**

- `docker-compose.yml` runs the whole stack — API and PostgreSQL — so anyone with Docker can start
  and test the app without the .NET SDK. `docker compose up -d postgres` still gives a database
  alone for `dotnet run` and debugging.
- The API container runs as in production: environment not set (so `Production`), configuration only
  through environment variables. The image that passes locally is the image that gets deployed.
- PostgreSQL `18-alpine`, the same tag in compose and in the test fixture. 18 is the current stable
  major and the default on Render; Neon offers it too. 19 is days from release and not yet offered
  by managed hosts. Changing the version later is a one-line edit in two places.
- The runtime image installs `curl` for the compose health check; the base image ships no HTTP
  client. The health check is declared in compose, not in the Dockerfile — hosting platforms probe
  over HTTP from outside.
- A connection string with a literal password appears only in `docker-compose.yml` and
  `appsettings.Development.json`. The `secrets-scan` hook blocks it anywhere else, including the
  README and test code.

**Tests**

- Integration tests run against real PostgreSQL in Testcontainers —
  [ADR 0002](../adr/0002-integration-tests-on-real-postgres.md). One container per test run, shared
  by all test classes.
- Tests run on Microsoft.Testing.Platform, opted in through `global.json` (`test.runner`). xUnit v3
  4.x supports only MTP v2, and `dotnet test` on the .NET 10 SDK refuses the VSTest mode for it, so
  `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` are not referenced.
- xUnit v3 with its built-in `Assert`. No assertion library: FluentAssertions is commercial since
  v8, and the plan avoids commercial dependencies.
- Tests override the connection string with `UseSetting` on the web host builder: `Program.cs` reads
  configuration before `WebApplicationFactory` configuration callbacks run.

**CI**

- One workflow, two jobs. `build-test` proves the code; `stack` proves that the command the README
  promises really produces a healthy stack. The image is built but not pushed — registry and deploy
  are step 9.

## Contract

### Projects and references

```
Freitty.slnx
global.json · Directory.Build.props · Directory.Packages.props
Dockerfile · .dockerignore · docker-compose.yml
src/
├── Freitty.Domain/            no references, no packages
├── Freitty.Application/       → Domain
├── Freitty.Infrastructure/    → Application
└── Freitty.Api/               → Application, Infrastructure
tests/
└── Freitty.Api.IntegrationTests/   → Api
.github/workflows/ci.yml
.claude/skills/stack-up/SKILL.md
```

### HTTP

| Endpoint | Healthy | Unhealthy | Checks |
|---|---|---|---|
| `GET /health/live` | `200` | — | none: answers whenever the process serves requests |
| `GET /health/ready` | `200` | `503` | `AppDbContext` can connect to PostgreSQL |

Default plain-text body (`Healthy` / `Unhealthy`).

### Configuration

| Setting | `dotnet run` | Compose `api` container |
|---|---|---|
| `ConnectionStrings:Postgres` | `appsettings.Development.json`, host `localhost` | environment variable, host `postgres` |
| URL | `http://localhost:5080` (`launchSettings.json`, `http` profile, no browser launch) | `http://localhost:8080` |

### Compose services

| Service | Image | Host port | Health check | Notes |
|---|---|---|---|---|
| `postgres` | `postgres:18-alpine` | `5432` | `pg_isready` | database, user and password all `freitty`; named volume for data — the 18 images changed the data directory, take the mount path from the image documentation |
| `api` | built from `Dockerfile` | `8080` | `curl` on `/health/ready` | starts after `postgres` is healthy |

Both health checks: 5 s interval, 3 s timeout, 10 retries.

### Dockerfile

Multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` to restore and publish `Freitty.Api` in `Release`,
`mcr.microsoft.com/dotnet/aspnet:10.0` to run it. Project files and the `Directory.*.props` /
`global.json` files are copied and restored before the sources, so a code change does not re-download
packages. Runs as the image's built-in non-root user, listens on `8080`. `.dockerignore` keeps
`bin/`, `obj/`, `.git`, `tests/`, `docs/` and `presentation/` out of the build context.

### CI — `.github/workflows/ci.yml`

Triggers: push to `main`, every pull request. A newer run on the same ref cancels the older one.
Permissions: `contents: read`.

| Job | Steps |
|---|---|
| `build-test` | checkout → setup .NET from `global.json` → `dotnet restore` → `dotnet format --verify-no-changes --no-restore` → `dotnet build -c Release --no-restore` → `dotnet test -c Release --no-build` |
| `stack` | checkout → `docker compose up -d --build --wait --wait-timeout 180` → `curl -fsS http://localhost:8080/health/ready` → on failure print `docker compose logs` → always `docker compose down -v` |

### Skill — `.claude/skills/stack-up/SKILL.md`

A project skill, invoked as `/stack-up`, that brings the stack up and reports its health:

1. `docker compose up -d --build --wait --wait-timeout 180`.
2. Confirm `GET http://localhost:8080/health/ready` returns `200`.
3. On success, report the service states from `docker compose ps` and the API URL.
4. On failure, show `docker compose ps` and the last 50 log lines of each service that is not
   healthy, and name the likely cause (port already in use, image build error, database not ready).
   Do not retry blindly.

It also documents the way down: `docker compose down` keeps the data; `docker compose down -v`
deletes the database volume and is run only when the user asks for a reset.

### Docs touched

- `README.md` — replace the stub with "Run locally": prerequisites (Docker; .NET 10 SDK only for
  `dotnet run` / `dotnet test`), the compose command, the two URLs, how to run the tests.
- `CLAUDE.md` — add a `Skills` section listing `stack-up`.

## Acceptance criteria

- [ ] On a clean clone, `dotnet build Freitty.slnx -c Release` finishes with 0 warnings and 0 errors.
- [ ] `dotnet format Freitty.slnx --verify-no-changes` exits 0.
- [ ] A deliberate violation of a `:warning` rule from `.editorconfig` (a block-scoped namespace)
      fails `dotnet build`. Checked once by hand, not committed. If the build ignores the rule, the
      matching `dotnet_diagnostic.<id>.severity` lines are added to `.editorconfig`.
- [ ] No `.csproj` contains a package version; `Freitty.Domain` has no package or project
      references; the references match the graph in *Contract*.
- [ ] With only Docker installed, `docker compose up -d --build --wait` exits 0, both services are
      `healthy`, and `curl http://localhost:8080/health/ready` returns `200`.
- [ ] `docker compose stop postgres` → `/health/ready` returns `503` while `/health/live` still
      returns `200`. `docker compose start postgres` → `/health/ready` returns `200` again without
      restarting the API.
- [ ] `docker compose up -d --wait postgres`, then `dotnet run --project src/Freitty.Api` →
      `http://localhost:5080/health/ready` returns `200`.
- [ ] `dotnet test` is green with Docker running and the compose stack stopped — the tests bring
      their own database.
- [ ] `docker compose logs api` shows the startup lines and no entry per health probe.
- [ ] Both CI jobs are green on the pushed commit.
- [ ] `/stack-up` brings the stack up from a stopped state and reports both services healthy.
- [ ] The README "Run locally" section, followed literally, produces a running app.

## Tests

- Readiness returns `200` when PostgreSQL is reachable — integration
- Readiness returns `503` when PostgreSQL is unreachable (connection string pointing at a closed
  port, short timeout) — integration
- Liveness returns `200` when PostgreSQL is unreachable — integration
- Startup fails with a message naming `ConnectionStrings__Postgres` when the connection string is
  missing — integration

## Out of scope

- FastEndpoints, `/api/v1`, `ProblemDetails`, OpenAPI + Scalar, the command pipeline,
  FluentValidation, Mapperly — step 4, with the first endpoint.
- Entities, `IAppDbContext`, EF configurations, migrations, table naming — step 2.
- `Freitty.Domain.Tests` — step 2, with the first domain rule to test.
- `TimeProvider` registration — the step with its first consumer.
- Seed data — step 3.
- `wwwroot` and static files — step 7.
- Image registry, deploy, CD, the hosting choice — step 9.
