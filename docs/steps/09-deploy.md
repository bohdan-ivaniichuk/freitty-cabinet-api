# 09 — Deploy and CD

> **Draft, not groomed.** Written ahead of the grooming session so it does not start from zero.
> Everything under *Decisions* is a proposal; *Open questions* is what the session has to settle.
> Nothing here is input for implementation yet.

## Goal

The application and its database run on a public URL, and a push to `main` that passes CI replaces
the running version without a manual step. The assignment is judged by this deployment.

## Decisions

- **The image that CI tested is the image that runs.** The `stack` job of step 1 already builds it
  and proves it healthy; this step pushes that image to a registry and deploys it by its digest.
- **One container, one managed PostgreSQL.** The pages are served by the API, so there is nothing
  else to host.
- **Configuration through environment variables only**, as in the compose file: the connection
  string, and the seed switch.
- **Migrations and the seed run at startup**
  ([ADR 0007](../adr/0007-migrations-applied-at-startup.md)). The platform's health check is
  `/health/ready`; a version that cannot migrate never becomes ready and does not replace the old
  one.
- **TLS ends at the platform** (step 1).
- **Secrets live in the platform and in GitHub secrets**, never in the repository. The
  `secrets-scan` hook stays the guard.

## Contract

### Workflow

`CI` (existing) → on `main`, after both jobs pass: build and push the image to the registry → tell
the platform to deploy that image → wait for `/health/ready` on the public URL → fail the workflow
if it does not answer.

### Environments

| | Local | Deployed |
|---|---|---|
| Database | compose `postgres:18-alpine` | managed PostgreSQL 18 |
| Connection string | compose file | platform secret |
| Seed | on | on |
| URL | `http://localhost:8080` | public HTTPS |

### Docs

`README.md` gets the public URL and a "Deployment" section: what runs where, how a release
happens, how to roll back.

## Acceptance criteria

- [ ] The public URL shows the dashboard with seeded data over HTTPS.
- [ ] A commit to `main` that passes CI is live without anyone touching the platform.
- [ ] A commit that breaks startup does not replace the running version, and the workflow is red.
- [ ] No secret is in the repository or in a workflow log.
- [ ] The README's deployment section, followed literally, explains how to roll back.

## Tests

- The post-deploy check in the workflow is the test: readiness on the public URL.

## Out of scope

- Several instances, zero-downtime schema changes, migration bundles — the path named in ADR 0007.
- Monitoring, alerting, backups beyond what the platform gives.
- A custom domain.

## Open questions

1. **Where.** Render, Railway or Fly.io for the container; the platform's own PostgreSQL or Neon.
   Free-tier terms change — they are to be looked up on the day, not recalled. What decides:
   PostgreSQL 18 available, a container deployed from an image, a deploy that waits for a health
   check, no credit card surprise.
2. **Cold starts.** Free tiers put an idle container to sleep and some suspend an idle database.
   The first request of a demo may then take tens of seconds. Accept and warm it up before the
   presentation, keep it awake with a scheduled ping, or pay for a month?
3. **Stale data.** The seed is relative to the day it ran. A database seeded a week before the
   presentation shows a week-old "today". Reseed before the demo — by what mechanism: recreate the
   database, or a guarded reseed switch?
4. **Registry.** GitHub Container Registry with the workflow's own token is the least setup. Does
   the chosen platform pull from it, and does the image need to be public?
5. **The database user.** Startup migrations need DDL rights. Is the single owner user of a managed
   database acceptable here, said plainly in the presentation?
6. **Rollback.** Redeploy the previous image: fine while migrations only add. State that as the
   rule?
7. **Deploy trigger.** A separate workflow started by the success of CI, or a third job in the same
   workflow limited to `main`?
