# 0007 — Migrations are applied by the API at startup

- Status: accepted
- Date: 2026-11-17

## Context

The schema has to reach three places: a developer's `docker compose up`, the test container, and the
deployed database. The app is one deployable on a free hosting tier, run as a single instance, and
the README promises a working stack from one command.

## Decision

The API applies pending EF Core migrations after the host is built and before it starts listening.
If that fails, the process exits.

## Alternatives

- **A migration bundle run by the pipeline before the new version is released** — the app never
  holds DDL rights, a failed migration stops the deploy before any instance changes, and the step
  is visible in CD. This is the right shape for several instances and a locked-down database user.
  Not taken now: it needs a second image or a compose service, a CD job with database access, and a
  separate home for the seed — infrastructure with no benefit for one instance.
- **A background service that migrates with retries, with readiness reporting "schema current"** —
  the instance stays alive while the database is away. Rejected: more moving parts, and every
  integration test would have to wait for readiness before its first request.

## Consequences

- One command gives a working database locally, in tests and in production; the seed of step 3 runs
  at the same point.
- A wrong deployment fails at boot, not on the first request — the same rule as the missing
  connection string in step 1. An instance cannot start while the database is unreachable; the
  platform restarts it. Readiness still reports a database that goes away after startup.
- The application's database user needs DDL rights.
- Instances starting together do not race: EF Core takes a database lock for the duration of a
  migration.
- A migration must stay compatible with the version that is still running during a deploy. With one
  instance and a restart this is not yet a constraint; it becomes one with rolling deploys.
- Moving to a bundle later means removing one call from startup and adding one step to the
  pipeline. Migrations and schema do not change.
