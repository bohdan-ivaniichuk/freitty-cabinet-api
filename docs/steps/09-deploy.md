# 09 — Deploy and CD

> The answers under *Open questions* are recommendations that await the owner's confirmation.

## Goal

The application and its database run on Azure at a public HTTPS URL, and a push to `main` that
passes CI replaces the running version without a manual step — with the very image CI tested,
and only once the new version has passed its probes. The demo is always warm, costs at most 15 USD
with tax for the whole time it runs — at most twelve days where VAT is charged — and one command
deletes all of it.

## Decisions

- **Azure Container Apps and Azure Database for PostgreSQL, in one resource group** —
  [ADR 0014](../adr/0014-azure-container-apps-and-managed-postgresql.md). Region `northeurope`:
  the cheapest of the candidates for the database, the same price as the others for everything
  else, and every SKU below is offered there (checked in stage 3 of the runbook before anything is
  paid for). `swedencentral` is the fallback; then every `northeurope` in the URL, the README and
  [ADR 0014](../adr/0014-azure-container-apps-and-managed-postgresql.md) becomes `swedencentral`.
  Rejected: `westeurope`, whose Container Apps rates are about 40% higher and which breaks the
  budget at worst.
- **The ceiling is 15 USD including tax, and the window follows from it.** The owner's billing
  country charges VAT unless the owner says otherwise (*Open questions*); at 20% the worst case
  stays under the ceiling for 12 days (13.39 USD) and not for 13 (15.04). So the demo lives at
  most 12 days from provisioning to teardown, and the budget amount is the ceiling before tax:
  `15 / (1 + VAT rate)`, 12.50 at 20%. Without VAT the window is 14 days (13.92).
- **One replica, always on: `min-replicas 1`, `max-replicas 1`, single revision mode.** The demo
  never cold-starts, and the sample-data refresh and its request gate hold exactly one process
  ([ADR 0015](../adr/0015-the-demo-rebuilds-its-sample-data.md)). Single revision mode moves the
  traffic to a new revision only after its probes pass; until then the old revision serves
  everything. Rejected: scale to zero — the first visitor would wait for the container and the
  startup rebuild.
- **0.25 vCPU and 0.5 GiB.** Measured locally with the same limits (`docker run --cpus 0.25
  --memory 512m`, PostgreSQL 18): ready 9 seconds after start, migrations and the rebuild
  included; 92 MiB in use after the first requests, 74 MiB idle at 0.5% of a core. The first
  request to `/api/v1/dashboard` took 2.6 seconds (JIT), the next ones well under one. Rejected:
  0.5 vCPU and 1 GiB — a faster first request, but 12.74 USD of compute at worst over 14 days,
  which breaks the budget.
- **HTTP probes, timed for the startup work.** The process listens only after the migrations and
  the startup rebuild, so the startup probe on `/health/live` covers both: 5 seconds before the
  first try, then every 10 seconds, 10 failures allowed — about 95 seconds, more than ten times
  the measured startup. Liveness on `/health/live` (checks nothing, so a database outage does not
  restart the replica); readiness on `/health/ready` (the database check). Health requests pass
  the request gate and are not billed as requests.
- **The image CI tested is the image that runs.** The `stack` job builds it once with Compose,
  proves it healthy against PostgreSQL 18, and on `main` pushes that same local image to the
  registry. The `deploy` job, after both jobs pass, deploys it by digest. Nothing is rebuilt.
- **Azure Container Registry Basic, pulled by a managed identity.** Rejected: GitHub Container
  Registry — a public package publishes the compiled application of a private repository, and a
  private one needs a personal access token stored in Azure. ACR costs 0.17 USD a day.
- **GitHub signs in with OIDC to a user-assigned managed identity.** Its federated credential
  trusts only `main` of this repository, in GitHub's immutable subject format (the repository was
  created after 15 July 2026, so its tokens carry owner and repository IDs). Roles: `AcrPush` on
  the registry, `Contributor` on the container app — not on the resource group, so CD cannot
  change or delete the database. It can read the app's secrets, as any contributor of the app can,
  so the federated credential is guarded like the password: one branch of one repository.
  GitHub keeps identifiers in repository variables; no secret exists. Rejected: an
  app registration — a tenant-level object that outlives the resource group; the managed identity
  is deleted with it. Rejected: a GitHub environment as the subject — environments in private
  repositories need a paid GitHub plan.
- **PostgreSQL 18, Burstable B1ms, 32 GiB, 7 days of backups, no high availability.** 18 is
  generally available on the flexible server (18.6 for new servers). 32 GiB is the minimum and
  includes 32 GiB of free backup storage; autogrow off. The server is created with an explicit
  `--storage-size 32` — the CLI default is 128.
- **Public access with the "Azure services" firewall rule, TLS verified.** The rule
  `0.0.0.0–0.0.0.0` admits clients hosted in Azure — in any tenant — and nothing else; the
  password and TLS do the rest. The connection string sets
  `SSL Mode=VerifyFull`: the image (Ubuntu 24.04) trusts both roots Azure uses, DigiCert Global
  Root G2 and Microsoft RSA Root Certificate Authority 2017 (checked in the image). Rejected:
  firewall rules for the app's outbound addresses — they can change on the Consumption plan;
  VNet integration — [ADR 0014](../adr/0014-azure-container-apps-and-managed-postgresql.md).
- **`pg_trgm` is allow-listed before the first start.** The migration `OrderListIndexes` runs
  `CREATE EXTENSION`; Azure refuses an extension that is not in the server parameter
  `azure.extensions`.
- **The database `freitty` is created during provisioning**; the application never creates a
  missing database ([ADR 0007](../adr/0007-migrations-applied-at-startup.md)).
- **The application connects as the server administrator `freittyadmin`.** Migrations need DDL
  and `CREATE EXTENSION`; a second role would need `psql`, a firewall hole for the owner's machine
  and a second password, for synthetic data. The password is 48 random hex digits behind a fixed
  `Pg1-` prefix (four character classes, as Azure requires; no `,`, `;`, `=` or quote, which the
  connection string and the CLI's secret parser would split on). It is generated in the shell,
  set on the server and passed to the Container Apps secret in the same few commands, with output
  off — never typed, printed or written to a file.
- **Configuration is environment variables only**, as in Compose: the connection string from the
  secret `postgres-connection`, `Seed__Enabled=true`, `Seed__Refresh=true`, and
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. The last one is needed: the ingress ends TLS and
  forwards plain HTTP with `X-Forwarded-Proto: https`, and the OpenAPI document builds its
  `servers` URL from the request. Without it the document says `http://<fqdn>/` and Scalar's
  "send request" calls HTTP from an HTTPS page. Checked locally against the image: with the
  variable and the header the document says `https://…/`. ASP.NET Core then trusts
  `X-Forwarded-For`/`-Proto` from any sender, which is safe here because the ingress is the only
  way in and overwrites `X-Forwarded-Proto`. No code changes.
- **The container template is a file in the repository**, `deploy/containerapp.yaml`: image,
  resources, environment, probes, scale. It is applied by the runbook; CD changes only the image.
  Changing a probe or a variable is an edit to the file and one runbook command.
- **Logs go to a Log Analytics workspace with a 0.1 GB daily cap**, 30 days of retention. The
  first 5 GB a month are free and 31 days of retention are included, so the cap keeps the cost at
  zero; the application logs a few lines per request and none for probes. A failed start is read
  in the workspace after the replica is gone. Rejected: `--logs-destination none` — only a live
  stream, nothing left after a crash loop.
- **CD is a third job in the existing workflow**, not a second workflow: the image lives on the
  `stack` job's runner, and job outputs pass its digest without a registry lookup. Runs on `main`
  are not cancelled halfway through a deploy; the newest waiting run wins.
- **A workload-profiles environment with the Consumption profile only.** It is the environment
  type Microsoft recommends for new environments; the Consumption-only type is legacy, gone from
  the portal, and bills the same Consumption meters, so it would trade a supported type for no
  saving. The price list also carries an `Environment Management Hour` meter (0.10 USD an hour);
  the billing article charges plan management only with a Dedicated profile, private endpoints or
  planned maintenance, none of which this environment has. That is unconfirmed for the new meter,
  so the day-one cost check below is mandatory.
- **The provisioning runs in the Azure CLI image, pinned to 2.93.0**, with its own login volume.
  The commands below are written for the CLI's current argument names (2.86 renamed several
  `postgres flexible-server` arguments), and the container keeps the login apart from any other
  Azure account on the machine.
- **A budget alert at subscription scope**, so it survives the deletion of the resource group and
  watches what is left. A budget only warns, a day late at best, and a new subscription may not be
  able to create one for up to 48 hours; the guard that always works is a calendar alarm on the
  teardown date.

## Contract

### Resources

All names below are the runbook's variables; `<suffix>` makes the two globally unique names
unique and is chosen once.

| Resource | Name | Settings |
|---|---|---|
| Resource group | `rg-freitty-demo` | `northeurope`; holds everything except the budget |
| Log Analytics workspace | `log-freitty-demo` | `PerGB2018`, 30 days, daily cap 0.1 GB |
| Container registry | `freittydemo<suffix>` | Basic, admin user off, RBAC role assignments |
| Managed identity | `id-freitty-pull` | `AcrPull` on the registry; used by the app to pull |
| Managed identity | `id-freitty-github` | `AcrPush` on the registry, `Contributor` on the app; federated credential `github-main` |
| PostgreSQL flexible server | `freitty-demo-<suffix>` | 18, Burstable `Standard_B1ms`, 32 GiB Premium SSD, autogrow off, 7-day backups, no HA, no geo backup, password auth only |
| Database | `freitty` | UTF8, default collation |
| Server parameter | `azure.extensions` | `pg_trgm` |
| Firewall rule | `allow-azure-services` | `0.0.0.0`–`0.0.0.0` |
| Container Apps environment | `cae-freitty-demo` | workload profiles, Consumption profile only, logs to the workspace |
| Container app | `freitty-cabinet` | template from `deploy/containerapp.yaml`; external ingress, target port 8080, HTTP redirected to HTTPS; single revision mode; secret `postgres-connection` |
| Budget | `freitty-demo` | subscription scope, annual grain from the first of this month, amount `15 / (1 + VAT rate)` in the billing currency, alerts at 50%, 75% and 90% actual |

### `deploy/containerapp.yaml` (new)

`__IMAGE__` is replaced by the runbook with the image reference. The container name `api` must
match the one in `az containerapp create`.

```yaml
# The container template of the deployed app; applied by the runbook (docs/deployment.md).
# CD changes only the image. Secrets are not here: the connection string is the app secret
# postgres-connection.
properties:
  template:
    containers:
      - name: api
        image: __IMAGE__
        resources:
          cpu: 0.25
          memory: 0.5Gi
        env:
          - name: ConnectionStrings__Postgres
            secretRef: postgres-connection
          - name: Seed__Enabled
            value: "true"
          - name: Seed__Refresh
            value: "true"
          # The ingress ends TLS; the OpenAPI servers URL must say https.
          - name: ASPNETCORE_FORWARDEDHEADERS_ENABLED
            value: "true"
        probes:
          # The API listens only after its migrations and the sample-data rebuild: ~95 s allowed.
          - type: Startup
            httpGet:
              path: /health/live
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
            timeoutSeconds: 3
            failureThreshold: 10
          - type: Liveness
            httpGet:
              path: /health/live
              port: 8080
            periodSeconds: 10
            timeoutSeconds: 3
            failureThreshold: 3
          - type: Readiness
            httpGet:
              path: /health/ready
              port: 8080
            periodSeconds: 10
            timeoutSeconds: 5
            failureThreshold: 3
    scale:
      minReplicas: 1
      maxReplicas: 1
```

`az containerapp update --yaml` sends a `PATCH` with this template: it replaces the containers
and scale, and leaves ingress, registries and secrets as they are.

### `docker-compose.yml`

The `api` service gets `image: freitty-cabinet-api:local` next to `build: .`, with the comment
`# CI pushes this image to the registry after the stack test.` Compose then tags the image it
builds with a fixed name the workflow can push. Nothing else changes; local use is unaffected.

### Workflow

`.github/workflows/ci.yml` after this step. `build-test` is unchanged; `stack` gains the push;
`deploy` is new. Action versions are the current majors, looked up on 18 November 2026:
`actions/checkout@v7` (7.0.1), `actions/setup-dotnet@v6` (6.1.0), `azure/login@v3` (3.1.0). The
runner image has Azure CLI 2.92.0, Docker Compose and curl.

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

# A run on main is never cancelled halfway through a deploy: the next one waits for it.
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.ref != 'refs/heads/main' }}

permissions:
  contents: read

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
      - run: dotnet restore
      - run: dotnet format --verify-no-changes --no-restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build

  stack:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write
    outputs:
      image: ${{ steps.push.outputs.image }}
    env:
      # Off until the Azure side exists (docs/deployment.md), and again after the teardown.
      PUBLISH: ${{ github.event_name == 'push' && github.ref == 'refs/heads/main' && vars.AZURE_CLIENT_ID != '' }}
    steps:
      - uses: actions/checkout@v7
      - run: docker compose up -d --build --wait --wait-timeout 180
      - run: curl -fsS http://localhost:8080/health/ready
      - if: env.PUBLISH == 'true'
        uses: azure/login@v3
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      # The image Compose just built and tested, pushed as it is; deployed by its digest.
      - if: env.PUBLISH == 'true'
        id: push
        env:
          ACR_NAME: ${{ vars.ACR_NAME }}
        run: |
          repository="$ACR_NAME.azurecr.io/freitty-cabinet-api"
          az acr login --name "$ACR_NAME"
          docker tag freitty-cabinet-api:local "$repository:$GITHUB_SHA"
          docker push "$repository:$GITHUB_SHA"
          image="$(docker inspect --format '{{range .RepoDigests}}{{println .}}{{end}}' "$repository:$GITHUB_SHA" | grep "^$repository@")"
          echo "image=$image" >> "$GITHUB_OUTPUT"
          echo "Pushed \`$image\`" >> "$GITHUB_STEP_SUMMARY"
      - if: failure()
        run: docker compose logs
      - if: always()
        run: docker compose down -v

  deploy:
    needs: [build-test, stack]
    # Off until the container app exists (docs/deployment.md, stage 9), and when no image was pushed.
    if: github.event_name == 'push' && github.ref == 'refs/heads/main' && vars.AZURE_CONTAINER_APP != '' && needs.stack.outputs.image != ''
    runs-on: ubuntu-latest
    # Room for the update (8), the wait (15) and the clean-up, so a timeout never skips the clean-up.
    timeout-minutes: 35
    permissions:
      id-token: write
    env:
      APP: ${{ vars.AZURE_CONTAINER_APP }}
      RG: ${{ vars.AZURE_RESOURCE_GROUP }}
      IMAGE: ${{ needs.stack.outputs.image }}
    steps:
      - uses: azure/login@v3
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}
      - name: Deploy the tested image
        id: deploy
        timeout-minutes: 8
        run: |
          suffix="c${GITHUB_SHA::7}-$GITHUB_RUN_ATTEMPT"
          echo "revision=$APP--$suffix" >> "$GITHUB_OUTPUT"
          az containerapp update --name "$APP" --resource-group "$RG" \
            --image "$IMAGE" --revision-suffix "$suffix" --output none
      # Single revision mode moves the traffic once the new revision passes its probes; a revision
      # that never does leaves the old one serving.
      - name: Wait for the new revision to take the traffic
        timeout-minutes: 15
        env:
          REVISION: ${{ steps.deploy.outputs.revision }}
        run: |
          for _ in $(seq 1 40); do
            # A poll that fails once is not a failed deploy: || true keeps the loop going.
            ready="$(az containerapp show --name "$APP" --resource-group "$RG" \
              --query properties.latestReadyRevisionName --output tsv || true)"
            if [ "$ready" = "$REVISION" ]; then exit 0; fi
            state="$(az containerapp revision show --name "$APP" --resource-group "$RG" \
              --revision "$REVISION" \
              --query "[properties.provisioningState, properties.runningState]" --output tsv \
              | tr '\t' ' ')"
            echo "$(date -u +%H:%M:%S) $REVISION: $state"
            case "$state" in *Failed*) exit 1 ;; esac
            sleep 15
          done
          echo "$REVISION did not become ready in 10 minutes"
          exit 1
      - name: Check the public URL
        run: |
          fqdn="$(az containerapp show --name "$APP" --resource-group "$RG" \
            --query properties.configuration.ingress.fqdn --output tsv)"
          curl -fsS --max-time 10 --retry 10 --retry-delay 6 --retry-all-errors "https://$fqdn/health/ready"
          curl -fsS --max-time 10 -o /dev/null "https://$fqdn/api/v1/dashboard"
          echo "Live at https://$fqdn/" >> "$GITHUB_STEP_SUMMARY"
      - name: Show why the revision failed and stop it
        if: (failure() || cancelled()) && steps.deploy.outputs.revision != ''
        timeout-minutes: 5
        env:
          REVISION: ${{ steps.deploy.outputs.revision }}
        run: |
          # Bounded, so that slow logs never keep the revision from being stopped.
          timeout 60 az containerapp logs show --name "$APP" --resource-group "$RG" \
            --type system --tail 50 || true
          timeout 60 az containerapp logs show --name "$APP" --resource-group "$RG" --revision "$REVISION" \
            --type console --tail 100 --format text || true
          ready="$(az containerapp show --name "$APP" --resource-group "$RG" \
            --query properties.latestReadyRevisionName --output tsv)"
          if [ "$ready" != "$REVISION" ]; then
            az containerapp revision deactivate --name "$APP" --resource-group "$RG" \
              --revision "$REVISION" || true
          fi
```

Notes for the implementer:

- `PUBLISH` and the `deploy` condition use repository *variables*, so the workflow stays green
  before provisioning and after the teardown: with the variables absent nothing is pushed or
  deployed.
- The `stack` job pushes on `main` while `build-test` may still run; an image whose unit tests
  then fail sits unused in the registry and is never deployed, because `deploy` needs both jobs.
- The revision suffix is `c` + the short SHA + the run attempt (a suffix must start with a
  letter and be unique per app). A re-run gets a new revision.
- The failure step runs on failure and on cancellation, and deactivates a revision that never
  became ready, so it stops restarting at the active rate. It never touches the revision that
  serves the traffic. System logs take neither `--revision` nor `--format text`; they cover the
  whole app.
- "Passed its probes" is all the platform checks. If the `curl`s fail after the traffic has
  moved, the new version stays live and the run is red; the owner reads the logs and rolls back
  or fixes forward (*Operations*).
- `az containerapp update` waits for the provisioning of the app, not of the revision; its step
  timeout bounds a hung call.
- No step prints a secret: the workflow never reads one, and `az containerapp update` runs with
  `--output none`.

### GitHub repository variables

Set under *Settings → Secrets and variables → Actions → Variables*, or with `gh variable set`.
None of them is a credential; OIDC needs no secret.

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | client ID of `id-freitty-github` |
| `AZURE_TENANT_ID` | `<tenant-id>` |
| `AZURE_SUBSCRIPTION_ID` | `<subscription-id>` |
| `AZURE_RESOURCE_GROUP` | `rg-freitty-demo` |
| `ACR_NAME` | `freittydemo<suffix>` |
| `AZURE_CONTAINER_APP` | `freitty-cabinet` — set last, in stage 9 |

### Environments

| | Local | Deployed |
|---|---|---|
| Database | Compose `postgres:18-alpine` | Azure Database for PostgreSQL flexible server 18, B1ms |
| Connection string | Compose file | Container Apps secret `postgres-connection`, TLS `VerifyFull` |
| Seed | on, refresh on | on, refresh on — rebuilt at every start (every deploy and restart) and after midnight in Toronto |
| Forwarded headers | off | `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` |
| URL | `http://localhost:8080` | `https://freitty-cabinet.<env-domain>.<region>.azurecontainerapps.io` |
| Logs | `docker compose logs` | Log Analytics `log-freitty-demo`, live stream with `az containerapp logs show` |

### Cost

Unit prices from the Azure Retail Prices API (`https://prices.azure.com/api/retail/prices`, USD,
pay-as-you-go, read on 18 November 2026), filtered by `serviceName` and `armRegionName`:

| Meter (`serviceName` / `meterName`) | `northeurope` | `swedencentral` | `polandcentral` | `westeurope` |
|---|---|---|---|---|
| Azure Database for PostgreSQL / `B1MS` (Burstable BS Series Compute) | 0.0180 /h | 0.0199 /h | 0.0199 /h | 0.0199 /h |
| Azure Database for PostgreSQL / `Storage Data Stored` (Flex Server Storage) | 0.1265 /GB-month | 0.1369 | 0.137 | 0.1369 |
| Azure Container Apps / `Standard vCPU Active Usage` | 0.000024 /s | 0.000024 | 0.000024 | 0.000034 |
| Azure Container Apps / `Standard vCPU Idle Usage` | 0.000003 /s | 0.000003 | 0.000003 | 0.000004 |
| Azure Container Apps / `Standard Memory Active Usage`, `… Idle Usage` | 0.000003 /GiB-s | 0.000003 | 0.000003 | 0.000004 |
| Azure Container Apps / `Environment Management Hour` (not expected to apply, see below) | 0.10 /h | 0.10 | 0.10 | 0.10 |
| Container Registry / `Basic Registry Unit` | 0.1666 /day | 0.1666 | 0.1666 | 0.1666 |
| Log Analytics / `Analytics Logs Data Ingestion` | 0 up to 5 GB, then 2.76 /GB | 2.99 after 5 GB | 2.99 after 5 GB | 2.99 after 5 GB |
| Bandwidth / `Standard Data Transfer Out` (routing preference Internet) | 0 up to 100 GB | — | — | — |

The Container Apps free grant is 180,000 vCPU-seconds, 360,000 GiB-seconds and 2 million requests
per subscription per calendar month (pricing page and the Container Apps billing article). One
replica of 0.25 vCPU and 0.5 GiB uses 21,600 vCPU-seconds and 43,200 GiB-seconds a day, so the
grant lasts 8.3 days. The idle rate applies while the replica handles no request, uses under 0.01
vCPU and receives under 1,000 bytes a second; the local measurement idled at 0.005 vCPU. Storage
is prorated over 730 hours a month; backup storage up to the provisioned 32 GiB is free.

`northeurope`, in USD. *Expected* bills the replica at the idle rate, *worst* at the active rate,
both after the free grant of one calendar month:

| Item | 7 days | 10 days | 12 days | 14 days |
|---|---|---|---|---|
| PostgreSQL compute, B1ms | 3.02 | 4.32 | 5.18 | 6.05 |
| PostgreSQL storage, 32 GiB | 0.93 | 1.33 | 1.60 | 1.86 |
| Container Registry Basic | 1.17 | 1.67 | 2.00 | 2.33 |
| Container Apps, idle (expected) | 0.00 | 0.32 | 0.71 | 1.10 |
| Container Apps, active (worst) | 0.00 | 1.08 | 2.38 | 3.67 |
| Log Analytics (capped at 0.1 GB/day), egress, requests | 0.00 | 0.00 | 0.00 | 0.00 |
| **Expected, before tax** | **5.12** | **7.64** | **9.49** | **11.35** |
| **Worst, before tax** | **5.12** | **8.40** | **11.16** | **13.92** |
| **Expected, with 20% VAT** | **6.15** | **9.17** | **11.39** | **13.61** |
| **Worst, with 20% VAT** | **6.15** | **10.08** | **13.39** | **16.70** |

- With 20% VAT the worst case holds for 12 days and fails at 13 (15.04); without VAT it holds for
  14. Hence the 12-day window.
- The daily cost is about 0.73 USD while the grant lasts (database 0.57, registry 0.17) and 0.93
  expected, 1.38 worst, after it.
- A window that crosses a month boundary gets a second grant and costs less than the table says.
- The 14-day worst case elsewhere: `swedencentral` and `polandcentral` 14.71, `westeurope` 16.18,
  all before tax.
- Each deploy runs a second replica for a few minutes: a few cents.
- **Unconfirmed risk:** the price list shows an `Environment Management Hour` meter for Container
  Apps (sku `Environment`, 0.10 USD an hour, effective 1 November 2026) next to `Environment
  Planned Maintenance Hour` and `Environment Private Endpoint`. The billing article charges plan
  management only with a Dedicated profile, and the pricing page ties the meter to the Dedicated
  plan, private endpoints and planned maintenance; no document names this meter for a
  Consumption-only workload. If it applied it would add 2.40 USD a day and break the budget in
  three days. The day-one check (*Day one*, item 7) decides; any `Environment …` line in the cost
  means teardown at once.

### Provisioning runbook

Copied verbatim into `docs/deployment.md`. Bash, in the CLI container, from the repository root.
**Owner** marks what only the owner can do. Every stage ends with a check; a stage whose check
fails is fixed before the next one starts — except the budget of stage 4, which may wait.

**Stage 0 — Subscription and billing (owner), two days ahead.** In the Azure portal, create a
pay-as-you-go subscription (card and a second factor) at least two days before the rest: a new
subscription may not be able to create a budget for up to 48 hours. Note `<tenant-id>` and
`<subscription-id>`. In *Cost Management + Billing*, read the billing account's currency and
whether invoices add VAT; they set `VAT_RATE` and the budget amount in stage 2. Do not enable
Microsoft Defender plans; they are not part of the budget. Put a calendar alarm on the teardown
date — provisioning day plus 12 (plus 14 without VAT).

**Stage 1 — The CLI, apart from any other Azure login (owner signs in).**

```bash
docker volume create freitty-azure
docker run -it --rm -v freitty-azure:/root/.azure -v "$PWD":/work -w /work \
  mcr.microsoft.com/azure-cli:2.93.0 bash
```

Inside the container:

```bash
az login --use-device-code --tenant <tenant-id>        # owner: browser, second factor
az account set --subscription <subscription-id>
az account show --query "{name:name, id:id, state:state}" --output table
```

Check: the subscription shown is the new one, state `Enabled`.

**Stage 2 — Variables.** Run this block at the start of every session — the container is
removed on exit, and the later stages often run in a new one. It only reads; values of resources
that do not exist yet stay empty.

```bash
TENANT_ID=<tenant-id>
SUBSCRIPTION_ID=<subscription-id>
LOCATION=northeurope
SUFFIX=<suffix>                     # 4–6 lowercase letters/digits, chosen once (Open questions)
VAT_RATE=0.20                       # 0 if the invoices carry no VAT
RG=rg-freitty-demo
LAW=log-freitty-demo
ACR=freittydemo$SUFFIX
ID_PULL=id-freitty-pull
ID_GITHUB=id-freitty-github
PG=freitty-demo-$SUFFIX
PG_ADMIN=freittyadmin
PG_DB=freitty
CAE=cae-freitty-demo                # not ENV: that name is special to POSIX shells
APP=freitty-cabinet
GITHUB_OWNER=<owner>
GITHUB_REPO=<repo>
GITHUB_OWNER_ID=<owner-id>          # stage 5 shows where it comes from; leave empty until then
GITHUB_REPO_ID=<repo-id>            # the same
FIRST_COMMIT=<sha>                  # full 40-character SHA of the commit stage 7 pushed; empty until then
ALERT_EMAIL=<owner-email>
BUDGET_AMOUNT="$(jq -n --argjson vat "$VAT_RATE" '15 / (1 + $vat) * 100 | floor / 100')"  # 12.5 at 20%

# Re-runnable helpers.
exists() { "$@" --output none >/dev/null 2>&1; }
value() { "$@" --output tsv 2>/dev/null || true; }   # empty when the resource is missing
require() { local name; for name; do [ -n "${!name}" ] || { echo "$name is empty"; return 1; }; done; }
grant() {  # role, principal object ID, scope; an empty scope would mean the whole subscription
  [ -n "$2" ] && [ -n "$3" ] || { echo "grant: empty principal or scope"; return 1; }
  [ -n "$(az role assignment list --assignee-object-id "$2" --role "$1" --scope "$3" \
    --query "[0].id" --output tsv)" ] || az role assignment create --assignee-object-id "$2" \
    --assignee-principal-type ServicePrincipal --role "$1" --scope "$3" --output none
}
new_password() { local hex; hex="$(openssl rand -hex 24)" || return 1; printf 'Pg1-%s' "$hex"; }

# Derived from what exists.
ACR_ID="$(value az acr show --resource-group "$RG" --name "$ACR" --query id)"
PULL_ID="$(value az identity show --resource-group "$RG" --name "$ID_PULL" --query id)"
PULL_PRINCIPAL="$(value az identity show --resource-group "$RG" --name "$ID_PULL" --query principalId)"
GITHUB_PRINCIPAL="$(value az identity show --resource-group "$RG" --name "$ID_GITHUB" --query principalId)"
GITHUB_CLIENT_ID="$(value az identity show --resource-group "$RG" --name "$ID_GITHUB" --query clientId)"
DIGEST="$(value az acr repository show --name "$ACR" --image "freitty-cabinet-api:$FIRST_COMMIT" --query digest)"
IMAGE="${DIGEST:+$ACR.azurecr.io/freitty-cabinet-api@$DIGEST}"
APP_ID="$(value az containerapp show --resource-group "$RG" --name "$APP" --query id)"
FQDN="$(value az containerapp show --resource-group "$RG" --name "$APP" --query properties.configuration.ingress.fqdn)"
```

**Stage 3 — Providers and the region (nothing billed yet).**

```bash
for ns in Microsoft.App Microsoft.OperationalInsights Microsoft.ContainerRegistry \
  Microsoft.DBforPostgreSQL Microsoft.ManagedIdentity Microsoft.Consumption; do
  az provider register --namespace "$ns" --wait
done
# PostgreSQL: the region is open to this subscription, B1ms and version 18 are offered.
az postgres flexible-server list-skus --location "$LOCATION" \
  --query "[0].supportedFeatures[?name=='OfferRestricted'].status | [0]" --output tsv
az postgres flexible-server list-skus --location "$LOCATION" \
  --query "[0].supportedServerEditions[?name=='Burstable'].supportedServerSkus[].name" --output tsv
az postgres flexible-server list-skus --location "$LOCATION" \
  --query "[0].supportedServerVersions[].name" --output tsv
az acr check-name --name "$ACR" --query nameAvailable --output tsv
```

Check: `Disabled`; the list contains `Standard_B1ms`; the versions contain `18`; `true` (`false`
once the registry exists — then it is ours). `list-skus` also prints a pricing notice on stderr
between the values; ignore it. If the region is restricted or lacks either, set
`LOCATION=swedencentral` and repeat this stage. If no candidate region offers 18, see *Open
questions*.

**Stage 4 — Resource group and budget.**

```bash
az group create --name "$RG" --location "$LOCATION" --output none
cat > /tmp/budget.json <<EOF
{
  "properties": {
    "category": "Cost",
    "amount": $BUDGET_AMOUNT,
    "timeGrain": "Annually",
    "timePeriod": { "startDate": "$(date -u +%Y-%m-01)T00:00:00Z", "endDate": "2027-12-01T00:00:00Z" },
    "notifications": {
      "actual-50": { "enabled": true, "operator": "GreaterThanOrEqualTo", "threshold": 50,
        "thresholdType": "Actual", "contactEmails": ["$ALERT_EMAIL"] },
      "actual-75": { "enabled": true, "operator": "GreaterThanOrEqualTo", "threshold": 75,
        "thresholdType": "Actual", "contactEmails": ["$ALERT_EMAIL"] },
      "actual-90": { "enabled": true, "operator": "GreaterThanOrEqualTo", "threshold": 90,
        "thresholdType": "Actual", "contactEmails": ["$ALERT_EMAIL"] }
    }
  }
}
EOF
az rest --method put --body @/tmp/budget.json --output none \
  --url "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/providers/Microsoft.Consumption/budgets/freitty-demo?api-version=2024-08-01"
```

- The amount is the ceiling before tax, in the billing currency: `15 / (1 + VAT rate)` converted
  from USD if the account bills in another currency (edit `BUDGET_AMOUNT`).
- The annual grain keeps one running total across the month boundary.
- Budgets are evaluated about once a day on cost data 8–24 hours old, and they stop nothing: the
  90% alert can arrive after the ceiling. The thresholds are early on purpose. A forecast alert is
  left out: on an annual grain the forecast passes 100% within days whatever happens.
- A new subscription may refuse the budget for up to 48 hours, in the CLI and in the portal
  (*Cost Management → Budgets → Add*, scope the subscription, reset period *Annually*, the same
  amount and three alerts) alike. If it does, go on with stage 5 and run this block again later;
  until it succeeds, the calendar alarm of stage 0 is the guard.

Check: `az group show --name "$RG" --query properties.provisioningState --output tsv` says
`Succeeded`; `az rest --method get --url` (the same URL) returns the budget with three
notifications — or the budget is noted as pending.

**Stage 5 — Logs, registry, identities, roles.** First, on the host with `gh` signed in (owner),
read the repository's IDs and its OIDC subject setting:

```bash
gh api repos/<owner>/<repo> --jq '{created_at, full_name, id, owner_id: .owner.id}'
gh api repos/<owner>/<repo>/actions/oidc/customization/sub
```

`id` and `owner_id` go to `GITHUB_REPO_ID` and `GITHUB_OWNER_ID` in stage 2 (run it again). A
repository created after 15 July 2026 with `"use_default": true` presents
`repo:<owner>@<owner-id>/<repo>@<repo-id>:ref:refs/heads/main`. If the second call shows custom
`include_claim_keys`, the subject is built from those keys instead — stop and work it out before
creating the credential. Then, in the container:

```bash
az monitor log-analytics workspace create --resource-group "$RG" --name "$LAW" \
  --location "$LOCATION" --sku PerGB2018 --retention-time 30 --quota 0.1 --output none
exists az acr show --resource-group "$RG" --name "$ACR" || \
  az acr create --resource-group "$RG" --name "$ACR" --location "$LOCATION" --sku Basic \
    --admin-enabled false --role-assignment-mode rbac --output none
az identity create --resource-group "$RG" --name "$ID_PULL" --location "$LOCATION" --output none
az identity create --resource-group "$RG" --name "$ID_GITHUB" --location "$LOCATION" --output none
```

Run stage 2 again — it derives the IDs used below — then:

```bash
require ACR_ID PULL_PRINCIPAL GITHUB_PRINCIPAL GITHUB_OWNER_ID GITHUB_REPO_ID && {
grant AcrPull "$PULL_PRINCIPAL" "$ACR_ID"
grant AcrPush "$GITHUB_PRINCIPAL" "$ACR_ID"
az identity federated-credential create --resource-group "$RG" --identity-name "$ID_GITHUB" \
  --name github-main --issuer https://token.actions.githubusercontent.com \
  --subject "repo:$GITHUB_OWNER@$GITHUB_OWNER_ID/$GITHUB_REPO@$GITHUB_REPO_ID:ref:refs/heads/main" \
  --audiences api://AzureADTokenExchange --output none
}
```

Check: `az role assignment list --scope "$ACR_ID" --query "[].roleDefinitionName" --output tsv`
lists `AcrPull` and `AcrPush`; `az identity federated-credential list --resource-group "$RG"
--identity-name "$ID_GITHUB" --query "[].subject" --output tsv` prints the subject with both IDs.
If a later `azure/login` fails with `AADSTS700213`, the message prints the subject GitHub
presented: create the credential again with exactly that subject.

**Stage 6 — PostgreSQL.** The server gets a throwaway password; stage 8 replaces it with the one
the app receives.

```bash
exists az postgres flexible-server show --resource-group "$RG" --name "$PG" || \
  az postgres flexible-server create --resource-group "$RG" --name "$PG" --location "$LOCATION" \
    --version 18 --tier Burstable --sku-name Standard_B1ms \
    --storage-type Premium_LRS --storage-size 32 --storage-auto-grow Disabled \
    --backup-retention 7 --geo-redundant-backup Disabled --zonal-resiliency Disabled \
    --password-auth Enabled --microsoft-entra-auth Disabled \
    --admin-user "$PG_ADMIN" --admin-password "$(new_password)" \
    --public-access None --yes --output none
# CLI 2.93 creates the server with public network access off, whatever its help says of None.
# Turn it on — with no rule yet, nothing can connect — then admit only Azure services.
az postgres flexible-server update --resource-group "$RG" --name "$PG" \
  --public-access Enabled --output none
az postgres flexible-server firewall-rule create --resource-group "$RG" --server-name "$PG" \
  --name allow-azure-services --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 --output none
az postgres flexible-server parameter set --resource-group "$RG" --server-name "$PG" \
  --name azure.extensions --value pg_trgm --output none
exists az postgres flexible-server db show --resource-group "$RG" --server-name "$PG" \
  --name "$PG_DB" || \
  az postgres flexible-server db create --resource-group "$RG" --server-name "$PG" \
    --name "$PG_DB" --output none
```

Check:

```bash
az postgres flexible-server show --resource-group "$RG" --name "$PG" \
  --query "{state:state, version:version, sku:sku.name, storageGb:storage.storageSizeGb}" --output table
az postgres flexible-server parameter show --resource-group "$RG" --server-name "$PG" \
  --name azure.extensions --query value --output tsv
az postgres flexible-server db list --resource-group "$RG" --server-name "$PG" \
  --query "[].name" --output tsv
```

Expected: `Ready`, `18`, `Standard_B1ms`, `32`; `pg_trgm`; the list contains `freitty`.

**Stage 7 — GitHub variables and the first image (owner, in GitHub).** Wait a few minutes after
stage 5 for the role assignments to propagate. Print the values (none is secret) and set the first
five variables of the table above; leave `AZURE_CONTAINER_APP` unset.

```bash
echo "AZURE_CLIENT_ID=$GITHUB_CLIENT_ID AZURE_TENANT_ID=$TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID AZURE_RESOURCE_GROUP=$RG ACR_NAME=$ACR"
```

On the host: `gh variable set <NAME> --body <value> --repo <owner>/<repo>` for each. Then push
this step's commit to `main`. Re-running an earlier `main` run works only if its commit already
contains the new workflow. The `stack` job signs in, pushes the image and writes `Pushed <image>`
to the run summary; `deploy` is skipped. If `az acr login` fails there, see *Day one*, item 4.

Check: set `FIRST_COMMIT` in stage 2 to that run's full 40-character commit SHA — the image tag;
a short one leaves `IMAGE` empty — run stage 2 again, and
`echo "$IMAGE"` prints the same reference as the run summary.

**Stage 8 — Environment and app; the password reaches only the secret.** Run stage 2 first. The
block runs in a subshell that stops at the first failure, and it starts by refusing to run with
any derived value empty — before it creates or changes anything. The password lives only inside
the subshell.

```bash
(
  set -euo pipefail
  require ACR_ID PULL_ID GITHUB_PRINCIPAL GITHUB_CLIENT_ID IMAGE

  exists az containerapp env show --resource-group "$RG" --name "$CAE" || \
    az containerapp env create --resource-group "$RG" --name "$CAE" --location "$LOCATION" \
      --enable-workload-profiles true --logs-destination log-analytics \
      --logs-workspace-id "$(az monitor log-analytics workspace show --resource-group "$RG" \
        --name "$LAW" --query customerId --output tsv)" \
      --logs-workspace-key "$(az monitor log-analytics workspace get-shared-keys \
        --resource-group "$RG" --name "$LAW" --query primarySharedKey --output tsv)" \
      --output none

  PG_PASSWORD="$(new_password)"
  az postgres flexible-server update --resource-group "$RG" --name "$PG" \
    --admin-password "$PG_PASSWORD" --output none
  # The secret name has 19 characters of the 20 allowed: do not lengthen it.
  az containerapp create --resource-group "$RG" --name "$APP" --environment "$CAE" \
    --image "$IMAGE" --container-name api --cpu 0.25 --memory 0.5Gi \
    --min-replicas 1 --max-replicas 1 --revisions-mode single \
    --ingress external --target-port 8080 --transport auto \
    --user-assigned "$PULL_ID" --registry-server "$ACR.azurecr.io" --registry-identity "$PULL_ID" \
    --secrets "postgres-connection=Host=$PG.postgres.database.azure.com;Port=5432;Database=$PG_DB;Username=$PG_ADMIN;Password=$PG_PASSWORD;SSL Mode=VerifyFull" \
    --env-vars ConnectionStrings__Postgres=secretref:postgres-connection Seed__Enabled=true \
      Seed__Refresh=true ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    --output none

  # The template with the HTTP probes: a second revision of the same image.
  sed "s|__IMAGE__|$IMAGE|" deploy/containerapp.yaml > /tmp/containerapp.yaml
  az containerapp update --resource-group "$RG" --name "$APP" --yaml /tmp/containerapp.yaml \
    --output none
)
```

If the block stops after the password was changed, run it again: a new password replaces the
old one. If the app already exists, use the password rotation (*Operations*) instead; if only
the last command failed, apply the template with the *Change the template* block
(*Operations*). Then run stage 2 again (it derives `APP_ID` and `FQDN`) and give CD its role:

```bash
grant Contributor "$GITHUB_PRINCIPAL" "$APP_ID"
```

Check:

```bash
az containerapp show --resource-group "$RG" --name "$APP" \
  --query "{latest:properties.latestRevisionName, ready:properties.latestReadyRevisionName}" --output table
az containerapp env show --resource-group "$RG" --name "$CAE" \
  --query "properties.workloadProfiles[].workloadProfileType" --output tsv
curl -fsS "https://$FQDN/health/ready"; echo
curl -fsS "https://$FQDN/api/v1/me"; echo
curl -fsS "https://$FQDN/openapi/v1.json" | jq '.servers'
curl -sI "http://$FQDN/" | head -1
```

Expected: `latest` equals `ready` (repeat after a minute until it does); the environment lists
only `Consumption`; `Healthy`; the current user as JSON; a `servers` URL that starts with
`https://`; a `301` redirect. The migration lines are in the log of the *first* revision, the one
`az containerapp create` made; on an empty database EF Core also logs one
`ERR Failed executing DbCommand` for `__ef_migrations_history` before creating it — normal, as the
README says:

```bash
az containerapp revision list --resource-group "$RG" --name "$APP" --all \
  --query "[].name" --output tsv
az containerapp logs show --resource-group "$RG" --name "$APP" --revision <first revision> \
  --type console --tail 100 --format text
```

The lines to find: the two migrations and `Rebuilt sample data: 70 orders`. If the start failed
on a certificate error, see *Open questions*.

**Stage 9 — Turn CD on (owner, in GitHub).** Set `AZURE_CONTAINER_APP=freitty-cabinet`, a few
minutes after the `Contributor` grant. Push the commit that adds the public URL to the README.
Check: the run's `deploy` job is green, its summary says `Live at https://…`, and this shows the
`c<sha>-1` revision with all the traffic:

```bash
az containerapp revision list --resource-group "$RG" --name "$APP" \
  --query "[].{name:name, active:properties.active, traffic:properties.trafficWeight}" --output table
```

### Day one

The checks that only the live subscription can answer, in the order the owner meets them. Each
says what to do if it fails.

1. **Tax and currency** (stage 0): the billing account's currency and VAT set `VAT_RATE` and
   `BUDGET_AMOUNT`, and so the window: 12 days with VAT, 14 without.
2. **Budget** (stage 4): created, or refused on a subscription younger than 48 hours — retry
   daily; the calendar alarm guards meanwhile.
3. **OIDC subject** (stages 5 and 7): the first `azure/login` succeeds. `AADSTS700213` prints the
   presented subject; recreate the federated credential with it.
4. **`az acr login` with only `AcrPush`** (first run of stage 7): it falls back to
   `<name>.azurecr.io` when it cannot read the registry resource. If the push still fails, grant
   `Reader` on the registry to `id-freitty-github` (`grant Reader "$GITHUB_PRINCIPAL" "$ACR_ID"`).
   The same run proves that a role on one resource lets `azure/login` select the subscription;
   if it does not, grant `Reader` on the resource group:
   `grant Reader "$GITHUB_PRINCIPAL" "$(az group show --name "$RG" --query id --output tsv)"`.
5. **TLS `VerifyFull`** (first start, stage 8): no certificate error in the first revision's log.
   Otherwise *Open questions*, item 8.
6. **Broken start** (after stage 9; the acceptance test below): time it. The public URL must keep
   answering throughout, and the broken revision must end deactivated.
7. **Cost by meter, 24 hours after the app is created — mandatory.** *Cost Management → Cost
   analysis*, scope the subscription, last 7 days, group by *Meter*. Only the PostgreSQL compute
   meter (`B1MS`), the PostgreSQL storage meter and `Basic Registry Unit` may carry cost (the
   Container Apps meters are inside the grant). **Any `Environment …` line means tear down at
   once.** Expected daily total: about 0.73 USD.
8. **The idle rate, after day 9** (the grant is spent): Container Apps should cost about 0.19 USD a
   day (idle) — 0.65 means the replica bills as active, and the worst-case column of the cost
   table applies.

### Operations

Also in `docs/deployment.md`, after the runbook; each block starts with the stage 2 variables.

- **Release:** a push to `main` that passes CI. Nothing else.
- **The migration rule.** Single revision mode is a rolling deploy: the new revision migrates and
  rebuilds the data while the old one still serves, and a revision that migrates and then fails
  leaves the old version running on the new schema until the next deploy. So every migration must
  work with the version that is running: additive only — new tables, nullable columns or columns
  with defaults, new indexes. A rename or a drop is two releases: stop using it, then remove it
  ([ADR 0007](../adr/0007-migrations-applied-at-startup.md)).
- **Rollback:** list the revisions with their images, then deploy an earlier image:

  ```bash
  az containerapp revision list --resource-group "$RG" --name "$APP" --all \
    --query "[].{name:name, created:properties.createdTime, image:properties.template.containers[0].image}" --output table
  az containerapp update --resource-group "$RG" --name "$APP" \
    --image "<image@sha256:… of the chosen revision>" --revision-suffix "rollback-$(date +%s)" --output none
  ```

  Safe when no migration was added between the two versions. Across a migration it is safe only
  if the old version can still start on the new schema: a new table that references an existing
  one makes the old version's startup `TRUNCATE` fail, a new `NOT NULL` column without a default
  makes its seed fail — then the rollback revision never becomes ready, the current one keeps
  serving, and the fix is a forward commit. The next push to `main` deploys `main` again, so a
  lasting rollback is a `git revert` pushed to `main`.
- **Reset the demo data** (before the presentation): restart the revision; the startup rebuild
  runs. The single replica is down for the length of a start, about ten seconds.

  ```bash
  az containerapp revision restart --resource-group "$RG" --name "$APP" \
    --revision "$(az containerapp show --resource-group "$RG" --name "$APP" \
      --query properties.latestReadyRevisionName --output tsv)"
  ```

  Then open the three pages once, so the JIT has run before anyone watches.
- **Read the log of a failed start:** live, while the revision restarts:

  ```bash
  az containerapp logs show --resource-group "$RG" --name "$APP" --revision <revision> \
    --type console --tail 100 --format text
  az containerapp logs show --resource-group "$RG" --name "$APP" --type system --tail 50
  ```

  The system log (probe failures, exit codes) covers the whole app and prints JSON. Afterwards,
  in the portal, *Container App → Monitoring → Logs*:

  ```kusto
  ContainerAppConsoleLogs_CL
  | where ContainerAppName_s == "freitty-cabinet" and RevisionName_s == "<revision>"
  | project TimeGenerated, Log_s
  | order by TimeGenerated asc
  ```

  From the CLI the same query needs `az extension add --name log-analytics`, then
  `az monitor log-analytics query --workspace <customerId> --analytics-query "<query>"`.
- **Rotate the database password:**

  ```bash
  (
    set -euo pipefail
    PG_PASSWORD="$(new_password)"
    az postgres flexible-server update --resource-group "$RG" --name "$PG" \
      --admin-password "$PG_PASSWORD" --output none
    az containerapp secret set --resource-group "$RG" --name "$APP" --output none \
      --secrets "postgres-connection=Host=$PG.postgres.database.azure.com;Port=5432;Database=$PG_DB;Username=$PG_ADMIN;Password=$PG_PASSWORD;SSL Mode=VerifyFull"
  )
  ```

  then restart the revision as above (a secret is read at start).
- **Change the template** (a probe, a variable, the size): edit `deploy/containerapp.yaml`, then
  apply it with the image now running:

  ```bash
  IMAGE="$(az containerapp show --resource-group "$RG" --name "$APP" \
    --query "properties.template.containers[0].image" --output tsv)"
  sed "s|__IMAGE__|$IMAGE|" deploy/containerapp.yaml > /tmp/containerapp.yaml
  az containerapp update --resource-group "$RG" --name "$APP" --yaml /tmp/containerapp.yaml --output none
  ```

  A change made with `az containerapp update` flags instead stays in the app's template even if
  its revision fails, and every later deploy carries it — so it is always made through the file.
- **Watch the cost:** *Cost Management → Cost analysis*, scope the subscription, daily, grouped by
  meter. About 0.73 USD a day while the free grant lasts (8.3 days), then 0.93 expected and 1.38
  at worst; data arrives 8–24 hours late.
- **Stop the meter, partly, without deleting:** stop the server and deactivate the serving
  revision. Neither touches the template. The storage (about 0.13 USD a day) and the registry
  (0.17) keep billing, and the server starts itself again after seven days.

  ```bash
  az postgres flexible-server stop --resource-group "$RG" --name "$PG"
  az containerapp revision deactivate --resource-group "$RG" --name "$APP" \
    --revision "$(az containerapp show --resource-group "$RG" --name "$APP" \
      --query properties.latestReadyRevisionName --output tsv)"
  ```

  To resume: `az postgres flexible-server start`, then `az containerapp revision activate` with
  the same revision. Prefer the teardown.

### Teardown

Also in `docs/deployment.md`. In this order:

1. **GitHub first (owner):** delete the six variables
   (`gh variable delete <NAME> --repo <owner>/<repo>`). `PUBLISH` and `deploy` turn off, so a push
   during or after the teardown stays green instead of failing at the login.
2. **Delete the resource group:**

   ```bash
   az group delete --name "$RG" --yes
   az resource list --output table
   ```

   The first command deletes everything billable: app, environment, workspace, registry, database
   and its backups, both identities with the federated credential and their role assignments. It
   takes about ten minutes. The second lists every resource left in the subscription; it must
   print nothing. In a hurry, `az group delete --name "$RG" --yes --no-wait` returns at once and
   the meters stop as the deletion runs; check with the second command later.
3. **Budget:** keep it until the next invoice shows the final amount, then
   `az rest --method delete --url` (the stage 4 URL).
4. **Local:** `docker volume rm freitty-azure` removes the CLI login.
5. **Subscription (owner, optional):** cancel it in the portal once the last invoice is paid.

What remains: the budget (free) until deleted, the subscription, the workflow file and
`deploy/containerapp.yaml` in the repository.

### Docs

- `docs/deployment.md` (new): the provisioning runbook, *Day one*, operations and teardown above,
  verbatim.
- `README.md`:
  - under the title, the public URL —
    `https://freitty-cabinet.<env-domain>.<region>.azurecontainerapps.io/` — with the Scalar link
    next to it;
  - a **Deployment** section: what runs where (one container app, one replica, PostgreSQL 18
    flexible server, the registry, the region, one resource group); how a release happens (push
    to `main` → tests and the Compose stack → the tested image pushed and deployed by digest →
    traffic moves when the new revision passes its probes → the workflow checks the public URL; a
    version that cannot start never takes traffic and the run is red); the migration rule; the
    demo data rebuilt at every deploy and after midnight in Toronto; how to roll back (the two
    commands and the rule about migrations); how to tear down (GitHub variables, then the one
    command); a link to `docs/deployment.md` for the rest.
- `docs/plan.md` §3: the deploy target is Azure Container Apps and Azure Database for PostgreSQL
  ([ADR 0014](../adr/0014-azure-container-apps-and-managed-postgresql.md)).

## Acceptance criteria

- [ ] `https://<fqdn>/` shows the dashboard with seeded data; `/orders.html` and an order page
      work; `http://<fqdn>/` redirects to HTTPS.
- [ ] `https://<fqdn>/openapi/v1.json` lists a `servers` URL that starts with `https://`, and a
      request sent from Scalar at `https://<fqdn>/scalar/` gets an answer.
- [ ] A push to `main` that passes CI is live without anyone touching Azure: the `deploy` job is
      green and the new `c<sha>` revision holds 100% of the traffic.
- [ ] The digest deployed is the one the `stack` job pushed (run summary vs
      `az containerapp show --query "properties.template.containers[0].image"`).
- [ ] A version that cannot start does not replace the running one. Done once by hand, in the CLI
      container: `az containerapp update --set-env-vars Cabinet__TimeZone=Not/AZone
      --revision-suffix broken-1 --no-wait` makes a revision that fails at startup; the public URL
      keeps answering from the old revision throughout; the failed revision shows `Failed` or
      never becomes ready, and `az containerapp revision deactivate` stops it; then the template
      is put back with the *Change the template* block. The deploy job's failure step uses the
      same `deactivate`.
- [ ] No secret is in the repository or in any workflow log; the database password was never on
      screen.
- [ ] The logs of a started revision can be read in the workspace with the query above.
- [ ] The budget exists at subscription scope with the tax-adjusted amount and three alerts (or,
      on a subscription younger than 48 hours, is retried until it does).
- [ ] 24 hours after the app is created, Cost analysis by meter shows only the two PostgreSQL
      meters and the registry unit, about 0.73 USD a day; after the free grant (day 9) the daily
      total is at most 1.40 USD.
- [ ] The teardown happens no later than the calendar alarm: 12 days after provisioning with VAT,
      14 without.
- [ ] Before provisioning and after the teardown, a push to `main` gives a green run with nothing
      pushed or deployed.
- [ ] `docs/deployment.md`, followed literally, gets from an empty subscription to a live URL,
      rolls back, and deletes everything.

## Tests

- No new automated test: there is no application code in this step. The `stack` job is the test
  of the image that ships; the `deploy` job's revision wait and the two `curl`s are the test of
  the deployment.
- The broken-start check of *Acceptance criteria*, by hand, once.
- Compatibility with a second PostgreSQL major, done during grooming: the API image against a
  throwaway `postgres:17-alpine` (17.11) — migrations, `CREATE EXTENSION pg_trgm`, the rebuild and
  every `GET` endpoint answered `200`. Recorded here for the fallback in *Open questions*.

## Out of scope

- Several replicas, autoscaling, zero-downtime schema changes beyond the additive rule, a
  migration bundle — the path in [ADR 0007](../adr/0007-migrations-applied-at-startup.md).
- A virtual network, private endpoints, a custom domain, a CDN.
- Infrastructure as code (Bicep, Terraform) beyond the container template file; the runbook is the
  record.
- Passwordless database access and a runtime identity for the app — with photo upload.
- Photo upload itself. When it comes: a Standard LRS StorageV2 account in the same resource group,
  a private container, and a user-assigned identity on the app with `Storage Blob Data
  Contributor` on that container
  ([ADR 0014](../adr/0014-azure-container-apps-and-managed-postgresql.md)).
- Monitoring and alerting beyond the log workspace and the budget; backups beyond the server's
  seven days.
- A GitHub environment with protection rules — needs a paid plan for a private repository.

## Open questions

Each with the recommended answer.

1. **VAT and the window.** Assumed: the invoices carry VAT at 20%. Then the demo lives at most 12
   days (worst case 13.39 USD with tax) and the budget amount is 12.50. Provision two days before
   the presentation, so ten days remain for the review. If the owner confirms no VAT, 14 days and
   a budget of 15.
2. **Region.** `northeurope`, for the lowest database price; `swedencentral` if stage 3 shows
   `northeurope` restricted for this subscription. `polandcentral` is the nearest to Central and
   Eastern Europe but costs the same as Sweden and gains nothing a demo can feel.
3. **Names and the suffix.** The names in *Resources*. For `<suffix>`, run
   `openssl rand -hex 3` once and keep the result in `docs/deployment.md` — names are not secret.
   Subscription and tenant IDs stay out of the repository.
4. **Registry.** ACR Basic, 2.00 USD for 12 days. If the budget must shrink, the alternative is a
   public GHCR package, at the price of a public image of a private repository.
5. **After the presentation.** Keep the demo until the assignment's review is over, never past the
   calendar alarm, then run the teardown the same day.
6. **PostgreSQL version.** 18 — generally available on the flexible server, 18.6 for new servers.
   If stage 3 finds no region offering it, use 17 after this check: change the tag to
   `postgres:17-alpine` in `docker-compose.yml` and in `PostgresFixture.Image` on a local branch,
   run `dotnet test` and `docker compose up -d --build --wait`, and use 17 only if both pass (the
   API part already passed during grooming). The branch is not merged; the local stack stays on 18.
7. **The application as the server administrator.** Accepted for a demo with synthetic data, and
   said so in the presentation. A separate role is the first step after it.
8. **TLS verification.** `SSL Mode=VerifyFull`. If the first start fails with a certificate
   validation error in the log, fall back to `SSL Mode=Require` (encrypted, server not verified)
   with the password rotation block, and record it in the README.
9. **Instance size.** 0.25 vCPU / 0.5 GiB. 0.5 / 1 GiB halves the 2.6-second first request but its
   14-day worst case is 22.98 USD before tax.
10. **Budget e-mail and currency.** The owner's address; the amount converted if the subscription
    bills in another currency than USD.
