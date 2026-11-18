# 0014 — The demo runs on Azure Container Apps and Azure Database for PostgreSQL

- Status: accepted
- Date: 2026-11-18

## Context

The assignment is judged by a deployed demo; the brief lists Render, Railway, Fly.io and Azure, or
anything else. The owner settled the frame: Microsoft Azure, on a new pay-as-you-go subscription
(the free account is not available), at most 15 USD including tax for one to two weeks, after
which everything is deleted. A visitor must not wait for a cold start. The repository is private,
nothing secret is committed, and GitHub should hold no long-lived cloud credential.

The application is one container that serves the API and the pages
([ADR 0012](0012-static-pages-served-by-the-api.md)), migrates and seeds its database at startup
([ADR 0007](0007-migrations-applied-at-startup.md),
[ADR 0015](0015-the-demo-rebuilds-its-sample-data.md)) and must run as a single instance: the
sample-data refresh and its request gate hold one process. It is built and tested on PostgreSQL 18
and creates the `pg_trgm` extension in a migration. Photo upload may be added to the assignment
later.

## Decision

Everything lives in one resource group in North Europe (Sweden Central if North Europe is closed
to the subscription; the region then changes wherever it is named):

- **Azure Container Apps, Consumption plan**, in a workload-profiles environment with only the
  Consumption profile — one app, external HTTPS ingress on port 8080, 0.25
  vCPU and 0.5 GiB, exactly one replica (`min = max = 1`), single revision mode, HTTP startup,
  liveness and readiness probes on `/health/live` and `/health/ready`. Logs go to a Log Analytics
  workspace with a daily cap.
- **Azure Database for PostgreSQL flexible server** — PostgreSQL 18, Burstable B1ms, 32 GiB, seven
  days of backups, no high availability. Public access limited by the firewall rule that admits
  Azure services, TLS verified by the client. `pg_trgm` is allow-listed. The application connects
  as the server's administrator; the password is generated, never shown, and handed straight to a
  Container Apps secret.
- **Azure Container Registry, Basic** — the image CI tested is pushed there and deployed by
  digest. The app pulls with a user-assigned managed identity holding `AcrPull`; no registry
  password exists.
- **GitHub Actions signs in through OIDC** to a second user-assigned managed identity with a
  federated credential for the `main` branch. It holds `AcrPush` on the registry and `Contributor`
  on the one container app, nothing more — it can deploy, and read the app's secrets, but cannot
  change the database or anything else. GitHub stores identifiers as variables, no secret.
- **A budget with e-mail alerts** at subscription scope, so it outlives the resource group, set
  to the ceiling before tax, and a calendar alarm on the teardown date.
- **A window that fits the ceiling:** twelve days from provisioning to teardown where VAT is
  charged, fourteen where it is not.

## Alternatives

- **Railway** — the strongest alternative: a container from an image and a PostgreSQL template
  in one project, little setup, a plan billed by usage. Not chosen by the owner: the database is a
  container on a volume rather than a managed server, a deploy from CI needs a long-lived Railway
  token in GitHub, and the demo would not show the cloud the .NET ecosystem centres on.
- **Render, Fly.io** — named by the brief. Their free tiers stop an idle service, which the owner
  ruled out; the paid tiers bring nothing the two above do not.
- **Azure App Service for Containers** — a plan billed for its whole size every hour, and
  deployment slots, its way to let a version in only once it is healthy, need a higher tier.
  Container Apps bills one always-on replica at the idle rate and gates every revision on its
  probes.
- **PostgreSQL in a container next to the app** — cheaper, but no backups, and a database on a
  network file share. A managed server is the honest production shape.
- **Virtual network between the app and the database** — no public endpoint on the server at all.
  Not taken: a delegated subnet, a private DNS zone and private access chosen when the server is
  created, for a demo whose data is synthetic and rebuilt every day. TLS and a 48-hex-digit password
  stand in.
- **GitHub Container Registry** — free for a public package, but a public image publishes the
  compiled application of a private repository. A private package needs a personal access token
  stored in Azure, a long-lived credential the owner asked to avoid. The registry costs about 0.17
  USD a day.
- **Passwordless Entra authentication to PostgreSQL** — no password anywhere. Not taken: it needs
  code to fetch and refresh tokens and an Entra administrator on the server. Revisit with photo
  upload, which brings a managed identity for the app anyway.

## Consequences

- The bill is about 9.49 USD expected and 11.16 at worst for 12 days before tax — 13.39 at worst
  with 20% VAT — and 13.92 at worst for 14 days without VAT (the arithmetic is in
  [step 9](../steps/09-deploy.md)). Most of it is the database, which bills whether used or not.
  The budget only warns, a day late; it stops nothing. Deleting the resource group stops every
  meter.
- One cost is unconfirmed: the price list carries a Container Apps `Environment Management Hour`
  meter (0.10 USD an hour) that the documentation ties only to Dedicated profiles, private
  endpoints and planned maintenance. A cost check by meter after the first day decides; if it
  appears, the demo is torn down.
- The deploy is rolling: the new revision migrates while the old one serves, so every migration
  must work with the running version — additive only. This amends
  [ADR 0007](0007-migrations-applied-at-startup.md).
- One replica is always warm. A deploy starts a second one beside it; traffic moves only when the
  new revision passes its probes, so a version that cannot migrate or seed never replaces the
  running one.
- The replica is small: the first request after a start waits for the JIT, about 2.6 seconds
  measured locally at the same CPU limit.
- The application's database user owns the schema and can drop it — the same user that migrations
  need ([ADR 0007](0007-migrations-applied-at-startup.md)). Accepted for a demo with synthetic
  data.
- Any resource running in Azure, in any tenant, may open a connection to the server; only the
  password and TLS keep it out. The firewall rule is the price of not building a network.
- Photos, when they come, go to Azure Blob Storage in the same resource group: a Standard LRS
  account, a private container, and a user-assigned identity on the app with `Storage Blob Data
  Contributor` on that container. The app needs no key, and the same identity can later replace
  the database password.
- Leaving Azure means a new registry login and deploy command in the workflow; the image, its
  configuration and its probes do not change.
