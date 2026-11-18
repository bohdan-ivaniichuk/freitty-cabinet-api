# Deployment

How the demo is provisioned on Azure, released, operated and deleted. The decisions behind it are
in [step 9](steps/09-deploy.md) and
[ADR 0014](adr/0014-azure-container-apps-and-managed-postgresql.md); the resources and their
settings are listed in the step's *Contract*.

## GitHub repository variables

Set under *Settings → Secrets and variables → Actions → Variables*, or with `gh variable set`.
None of them is a credential; OIDC needs no secret.

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | client ID of `id-freitty-github` |
| `AZURE_TENANT_ID` | `<tenant-id>` |
| `AZURE_SUBSCRIPTION_ID` | `<subscription-id>` |
| `AZURE_RESOURCE_GROUP` | `rg-freitty-demo` |
| `ACR_NAME` | `freittydemoa0cdaa` |
| `AZURE_CONTAINER_APP` | `freitty-cabinet` — set last, in stage 9 |

## Provisioning runbook

Bash, in the CLI container, from the repository root.
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
SUFFIX=a0cdaa                       # chosen once with `openssl rand -hex 3`; names are not secret
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
`LOCATION=swedencentral` and repeat this stage. If no candidate region offers 18, see
[step 9, *Open questions*](steps/09-deploy.md#open-questions).

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

The lines to find: the two migrations and `Rebuilt sample data: 71 orders`. If the start failed
on a certificate error, see [step 9, *Open questions*](steps/09-deploy.md#open-questions).

**Stage 9 — Turn CD on (owner, in GitHub).** Set `AZURE_CONTAINER_APP=freitty-cabinet`, a few
minutes after the `Contributor` grant. Push the commit that adds the public URL to the README.
Check: the run's `deploy` job is green, its summary says `Live at https://…`, and this shows the
`c<sha>-1` revision with all the traffic:

```bash
az containerapp revision list --resource-group "$RG" --name "$APP" \
  --query "[].{name:name, active:properties.active, traffic:properties.trafficWeight}" --output table
```

## Day one

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
   Otherwise [step 9, *Open questions*](steps/09-deploy.md#open-questions), item 8.
6. **Broken start** (after stage 9; the acceptance test in
   [step 9](steps/09-deploy.md#acceptance-criteria)): time it. The public URL must keep answering
   throughout, and the broken revision must end deactivated.
7. **Cost by meter, 24 hours after the app is created — mandatory.** *Cost Management → Cost
   analysis*, scope the subscription, last 7 days, group by *Meter*. Only the PostgreSQL compute
   meter (`B1MS`), the PostgreSQL storage meter and `Basic Registry Unit` may carry cost (the
   Container Apps meters are inside the grant). **Any `Environment …` line means tear down at
   once.** Expected daily total: about 0.73 USD.
8. **The idle rate, after day 9** (the grant is spent): Container Apps should cost about 0.19 USD a
   day (idle) — 0.65 means the replica bills as active, and the worst-case column of the
   [cost table](steps/09-deploy.md#cost) applies.

## Operations

Each block starts with the stage 2 variables.

- **Release:** a push to `main` that passes CI. Nothing else.
- **The migration rule.** Single revision mode is a rolling deploy: the new revision migrates and
  rebuilds the data while the old one still serves, and a revision that migrates and then fails
  leaves the old version running on the new schema until the next deploy. So every migration must
  work with the version that is running: additive only — new tables, nullable columns or columns
  with defaults, new indexes. A rename or a drop is two releases: stop using it, then remove it
  ([ADR 0007](adr/0007-migrations-applied-at-startup.md)).
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
  Measured on the live app: a healthy revision takes over without a failed request (one request
  a second). Putting the template back after a revision that failed to start answered `404`
  for some seconds while the traffic moved; the failed revision itself never took traffic in
  the five minutes it was left trying.
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

## Teardown

In this order:

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
