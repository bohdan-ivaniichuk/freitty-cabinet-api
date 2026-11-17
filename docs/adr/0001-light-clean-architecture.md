# 0001 — Light Clean Architecture in four projects

- Status: accepted
- Date: 2026-11-17

## Context

The backend serves three screens: mostly reads, one write flow. The code is reviewed and presented,
so the structure has to be explainable in a minute and must not cost more than the features it
holds. The domain still has real rules — a status machine, quantity delta, alerts — that should be
testable without a database or a web host.

## Decision

Four projects with one dependency direction, enforced by project references:

```
Freitty.Api ──► Freitty.Application ──► Freitty.Domain
     │                  ▲
     └──► Freitty.Infrastructure
```

- **Domain** — entities, enums, domain rules. No package or project references.
- **Application** — queries and commands with their handlers, DTOs, validators, and `IAppDbContext`.
- **Infrastructure** — EF Core `DbContext`, configurations, migrations, seed, Dapper queries.
- **Api** — endpoints, host configuration, static frontend. The only project that references
  Infrastructure, and only to register it.

"Light" means the ceremony stops there: Application depends on `IAppDbContext` and queries it
directly, read handlers project straight to DTOs, and there is no generic repository, no MediatR and
no value object per field.

## Alternatives

- **One project with vertical slices** — the least code, and a fair fit for this size. Rejected
  because nothing would stop a handler from reaching into EF or HTTP types, and the dependency rule
  would rest on discipline instead of the compiler.
- **Full Clean Architecture** (repository per aggregate, domain events, a use-case interface per
  operation) — every read would cross three abstractions to run one `Select`. The read side is the
  bulk of this system, and those layers would hide the SQL the project is meant to show.
- **Application without `IAppDbContext`, handlers using the `DbContext` directly** — simpler, but it
  makes Application depend on Infrastructure and removes the one seam that keeps the domain and the
  handlers independent of the provider.

## Consequences

- The dependency rule is checked by the compiler: Domain cannot see EF Core, Application cannot see
  Npgsql or ASP.NET Core.
- Application references EF Core abstractions (`DbSet`, `IQueryable` extensions). That is accepted:
  hiding EF behind a repository would cost the projections the read side relies on.
- A new operation touches up to three projects. One folder per operation keeps each part easy to
  find.
- The first steps leave some projects nearly empty; they fill as the plan proceeds.
