# 0002 — Integration tests run on real PostgreSQL

- Status: accepted
- Date: 2026-11-17

## Context

The behaviour most worth testing here lives in the database: `ILIKE` search backed by `pg_trgm`,
filters and pagination translated to SQL, optimistic concurrency on a version column, Dapper
queries with `date_trunc` and `FILTER`. A test that does not run that SQL on PostgreSQL does not test it.

## Decision

Integration tests call the HTTP API through `WebApplicationFactory` against PostgreSQL started by
Testcontainers. One container per test run, shared by all test classes; the image tag is the same as
in `docker-compose.yml`. No other database provider is used in tests.

## Alternatives

- **EF Core InMemory provider** — not relational: no SQL translation, no constraints, no
  transactions. Queries that fail on PostgreSQL pass, and Dapper cannot run at all.
- **SQLite in-memory** — relational, but a different dialect: no `ILIKE`, no `pg_trgm`, no
  sequences, different collation and date handling. The tests would cover a database the app never runs on.
- **A shared PostgreSQL from docker-compose** — real, but the tests would depend on a stack started
  by hand and on whatever state the last run left behind. Testcontainers gives each run a clean
  database and needs nothing but Docker.

## Consequences

- Docker is required to run the integration tests, locally and in CI. GitHub-hosted Linux runners
  provide it.
- A test run pays a few seconds once to start the container. Sharing the container keeps that cost
  flat as tests are added; in exchange, tests must create the data they assert on and not rely on
  rows left by another test.
- The PostgreSQL version is pinned in two places — compose and the test fixture — and they must
  change together.
