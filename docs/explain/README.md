# Query plans

The three indexes of the Order List ([step 4](../steps/04-order-list.md), migration
`OrderListIndexes`) and the one of its board ([step 11](../steps/11-pipeline.md), migration
`PipelineIndex`) with `EXPLAIN (ANALYZE, BUFFERS)` before and after, on a data set large enough for
the planner to care. The seed's seventy-one orders are not: on them every plan is a sequential scan
and every one is fast.

| Plan | Index | Before | After |
|---|---|---|---|
| [`list-default.md`](list-default.md) — the default list, first page | `ix_orders_company_id_scheduled_at_id` | 4.3 ms, 2 391 buffers, Sort | 0.04 ms, 4 buffers, no Sort |
| [`search-number.md`](search-number.md) — `search=FR0000150` | `ix_orders_number_trgm` | 5.3 ms, 2 414 buffers | 2.5 ms, 150 buffers |
| [`search-ref-number.md`](search-ref-number.md) — `search=REF-0000224` | `ix_shipments_ref_number_trgm` | 47.9 ms, 5 423 buffers | 5.8 ms, 364 buffers |
| [`pipeline.md`](pipeline.md) — the board's totals and first cards ([step 11](../steps/11-pipeline.md)) | `ix_orders_company_id_status_scheduled_at_id` | 6.5 + 7.4 ms, 2 385 + 4 779 buffers | 0.66 + 1.0 ms, 36 + 78 buffers |

## The data

[`generate-volume.sql`](generate-volume.sql) fills a migrated, unseeded database: 50 companies of
4 000 orders each (200 000), 300 000 shipments, 360 000 cargo lines, 590 000 Operations and 150 000
invoices; `orders` and `shipments` are about 40 MB each. Every constraint of the schema holds; the
shape is described at the top of the script. Nothing of it reaches the application or the Compose
database.

The statements are measured for "Volume company 0", the company of the first Admin, which is the
user the API acts as. Each is the second of two runs, so the cache is warm, on PostgreSQL 18.7
(`postgres:18-alpine`) in Docker on an Apple-silicon laptop. Timings move from run to run; the
shape of the plans and the buffer counts do not.

## `UNION` against `OR EXISTS`

The search is written as a `UNION` of order ids, one branch per column. The obvious form puts both
conditions on the order:

```sql
SELECT o.id
FROM orders AS o
WHERE o.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND (
    o.number ILIKE '%REF-0000224%' ESCAPE '\'
    OR EXISTS (
        SELECT 1
        FROM shipments AS s
        WHERE s.order_id = o.id AND s.ref_number ILIKE '%REF-0000224%' ESCAPE '\'))
ORDER BY o.scheduled_at DESC, o.id DESC
LIMIT 12 OFFSET 0
```

With both trigram indexes in place, PostgreSQL runs the `EXISTS` once as a hashed subplan through
`ix_shipments_ref_number_trgm`, but the `OR` cannot be answered by an index on `orders`: it walks
every order of the company through `ix_orders_company_id_scheduled_at_id` and filters 3 999 of the
4 000 — 9.7 ms and 4 350 buffers, against 5.8 ms and 364 buffers for the `UNION` in
[`search-ref-number.md`](search-ref-number.md). The `OR` form reads the whole company whatever the
text, so its cost grows with the company; the `UNION` reads only what the indexes find.

## How to repeat

From the repository root, with Docker running and nothing else on port 55432:

1. A throwaway PostgreSQL 18. It trusts every connection, so no password is needed — acceptable
   only for a container that lives for one measurement:

   ```bash
   docker run -d --name freitty-explain -e POSTGRES_HOST_AUTH_METHOD=trust -p 55432:5432 postgres:18-alpine
   ```

2. Migrate it by starting the API once with the seed off, then stop it (Ctrl+C) when it listens:

   ```bash
   ASPNETCORE_ENVIRONMENT=Development Seed__Enabled=false \
   ConnectionStrings__Postgres='Host=localhost;Port=55432;Database=postgres;Username=postgres' \
   dotnet run --project src/Freitty.Api --no-launch-profile --urls http://localhost:5082
   ```

3. Generate the data (about half a minute):

   ```bash
   docker exec -i freitty-explain psql -U postgres -v ON_ERROR_STOP=1 < docs/explain/generate-volume.sql
   ```

4. To see the statements EF Core sends, start the API as in step 2 — `Development` logs the SQL — and
   call `http://localhost:5082/api/v1/orders`, `…?search=FR0000150` and `…?search=REF-0000224`. The company
   id is `SELECT id FROM companies WHERE name = 'Volume company 0'`; ids differ on every generation,
   numbers and Ref Ns do not.

5. Measure each statement twice and keep the second run. "After" as it is; "before" without its index,
   in a transaction that puts the index back:

   ```sql
   BEGIN;
   DROP INDEX ix_shipments_ref_number_trgm;
   EXPLAIN (ANALYZE, BUFFERS) SELECT …;
   ROLLBACK;
   ```

6. Remove the container: `docker rm -f freitty-explain`.
