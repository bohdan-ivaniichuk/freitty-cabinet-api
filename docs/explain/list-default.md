# The default list

The page query of `GET /api/v1/orders` without filters: the first twelve ids of one company, newest
Scheduled date first. Dropped for "before": `ix_orders_company_id_scheduled_at_id`.

The statement is the one EF Core sends, copied from the SQL log of `dotnet run` with the parameters
replaced by literals for "Volume company 0" (4 000 of the 200 000 orders). How the data was made and
how to repeat the measurement: [README](README.md).

```sql
SELECT o.id
FROM orders AS o
WHERE o.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'
ORDER BY o.scheduled_at DESC, o.id DESC
LIMIT 12 OFFSET 0
```

## Before — without `ix_orders_company_id_scheduled_at_id`

Measured as `BEGIN; DROP INDEX ix_orders_company_id_scheduled_at_id; EXPLAIN (ANALYZE, BUFFERS) …; ROLLBACK;`.

```
 Limit  (cost=5293.39..5293.42 rows=12 width=24) (actual time=4.180..4.183 rows=12.00 loops=1)
   Buffers: shared hit=2391
   ->  Sort  (cost=5293.39..5303.38 rows=3993 width=24) (actual time=4.179..4.180 rows=12.00 loops=1)
         Sort Key: scheduled_at DESC, id DESC
         Sort Method: top-N heapsort  Memory: 26kB
         Buffers: shared hit=2391
         ->  Bitmap Heap Scan on orders o  (cost=159.37..5201.85 rows=3993 width=24) (actual time=0.793..3.693 rows=4000.00 loops=1)
               Recheck Cond: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
               Heap Blocks: exact=2353
               Buffers: shared hit=2385
               ->  Bitmap Index Scan on ix_orders_company_id_number  (cost=0.00..158.37 rows=3993 width=0) (actual time=0.586..0.586 rows=4000.00 loops=1)
                     Index Cond: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                     Index Searches: 1
                     Buffers: shared hit=32
 Planning:
   Buffers: shared hit=191
 Planning Time: 0.724 ms
 Execution Time: 4.252 ms
```

## After

```
 Limit  (cost=0.42..1.12 rows=12 width=24) (actual time=0.022..0.024 rows=12.00 loops=1)
   Buffers: shared hit=4
   ->  Index Only Scan using ix_orders_company_id_scheduled_at_id on orders o  (cost=0.42..234.30 rows=3993 width=24) (actual time=0.021..0.022 rows=12.00 loops=1)
         Index Cond: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
         Heap Fetches: 0
         Index Searches: 1
         Buffers: shared hit=4
 Planning:
   Buffers: shared hit=224
 Planning Time: 0.675 ms
 Execution Time: 0.044 ms
```

## What changed

Before, PostgreSQL fetched all 4 000 orders of the company (2 391 buffers) and kept the top twelve
with a heapsort. After, it reads the first twelve entries of the index in order: no Sort node under
the Limit, 4 buffers, an index-only scan, and the cost no longer grows with the size of the company.
