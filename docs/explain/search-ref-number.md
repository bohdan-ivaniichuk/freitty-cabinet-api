# Search by Ref N

The page query of `GET /api/v1/orders?search=REF-0000224`: the `UNION` of all three branches — the text
ends in a hyphen and digits, so the label branch looks for an order numbered `REF` with a shipment at
position 224. Dropped for "before": `ix_shipments_ref_number_trgm`.

The statement is the one EF Core sends, copied from the SQL log of `dotnet run` with the parameters
replaced by literals for "Volume company 0" (4 000 of the 200 000 orders). How the data was made and
how to repeat the measurement: [README](README.md).

```sql
SELECT o.id
FROM orders AS o
WHERE o.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND o.id IN (
    SELECT o0.id
    FROM orders AS o0
    WHERE o0.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND o0.number ILIKE '%REF-0000224%' ESCAPE '\'
    UNION
    SELECT s.order_id AS id
    FROM orders AS o1
    INNER JOIN shipments AS s ON o1.id = s.order_id
    WHERE o1.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND s.ref_number ILIKE '%REF-0000224%' ESCAPE '\'
    UNION
    SELECT o2.id
    FROM orders AS o2
    WHERE o2.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND o2.number ILIKE 'REF' ESCAPE '\' AND EXISTS (
        SELECT 1
        FROM shipments AS s0
        WHERE o2.id = s0.order_id AND s0.position = 224)
)
ORDER BY o.scheduled_at DESC, o.id DESC
LIMIT 12 OFFSET 0
```

## Before — without `ix_shipments_ref_number_trgm`

Measured as `BEGIN; DROP INDEX ix_shipments_ref_number_trgm; EXPLAIN (ANALYZE, BUFFERS) …; ROLLBACK;`.

```
 Limit  (cost=8351.81..8351.81 rows=1 width=24) (actual time=45.490..47.705 rows=1.00 loops=1)
   Buffers: shared hit=2429 read=2994
   ->  Sort  (cost=8351.81..8351.81 rows=1 width=24) (actual time=45.489..47.703 rows=1.00 loops=1)
         Sort Key: o.scheduled_at DESC, o.id DESC
         Sort Method: quicksort  Memory: 25kB
         Buffers: shared hit=2429 read=2994
         ->  Nested Loop  (cost=8326.88..8351.80 rows=1 width=24) (actual time=45.467..47.681 rows=1.00 loops=1)
               Buffers: shared hit=2423 read=2994
               ->  Unique  (cost=8326.46..8326.47 rows=3 width=16) (actual time=45.441..47.654 rows=1.00 loops=1)
                     Buffers: shared hit=2419 read=2994
                     ->  Sort  (cost=8326.46..8326.46 rows=3 width=16) (actual time=45.439..47.652 rows=1.00 loops=1)
                           Sort Key: o0.id
                           Sort Method: quicksort  Memory: 25kB
                           Buffers: shared hit=2419 read=2994
                           ->  Append  (cost=81.94..8326.43 rows=3 width=16) (actual time=0.750..47.644 rows=1.00 loops=1)
                                 Buffers: shared hit=2419 read=2994
                                 ->  Bitmap Heap Scan on orders o0  (cost=81.94..158.46 rows=1 width=16) (actual time=0.151..0.152 rows=0.00 loops=1)
                                       Recheck Cond: ((number)::text ~~* '%REF-0000224%'::text)
                                       Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                       Buffers: shared hit=24
                                       ->  Bitmap Index Scan on ix_orders_number_trgm  (cost=0.00..81.93 rows=20 width=0) (actual time=0.148..0.148 rows=0.00 loops=1)
                                             Index Cond: ((number)::text ~~* '%REF-0000224%'::text)
                                             Index Searches: 1
                                             Buffers: shared hit=24
                                 ->  Gather  (cost=1000.42..8039.88 rows=1 width=16) (actual time=0.597..47.462 rows=1.00 loops=1)
                                       Workers Planned: 2
                                       Workers Launched: 2
                                       Buffers: shared hit=2386 read=2994
                                       ->  Nested Loop  (cost=0.42..7039.78 rows=1 width=16) (actual time=28.159..42.996 rows=0.33 loops=3)
                                             Buffers: shared hit=2386 read=2994
                                             ->  Parallel Seq Scan on shipments s  (cost=0.00..6938.50 rows=12 width=16) (actual time=28.150..42.986 rows=0.33 loops=3)
                                                   Filter: ((ref_number)::text ~~* '%REF-0000224%'::text)
                                                   Rows Removed by Filter: 100000
                                                   Buffers: shared hit=2382 read=2994
                                             ->  Index Scan using pk_orders on orders o1  (cost=0.42..8.44 rows=1 width=16) (actual time=0.023..0.023 rows=1.00 loops=1)
                                                   Index Cond: (id = s.order_id)
                                                   Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                                   Index Searches: 1
                                                   Buffers: shared hit=4
                                 ->  Nested Loop  (cost=43.50..128.07 rows=1 width=16) (actual time=0.027..0.028 rows=0.00 loops=1)
                                       Buffers: shared hit=9
                                       ->  Bitmap Heap Scan on orders o2  (cost=43.08..119.61 rows=1 width=16) (actual time=0.026..0.026 rows=0.00 loops=1)
                                             Recheck Cond: ((number)::text ~~* 'REF'::text)
                                             Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                             Buffers: shared hit=9
                                             ->  Bitmap Index Scan on ix_orders_number_trgm  (cost=0.00..43.08 rows=20 width=0) (actual time=0.019..0.019 rows=0.00 loops=1)
                                                   Index Cond: ((number)::text ~~* 'REF'::text)
                                                   Index Searches: 1
                                                   Buffers: shared hit=9
                                       ->  Index Only Scan using ix_shipments_order_id_position on shipments s0  (cost=0.42..4.44 rows=1 width=16) (never executed)
                                             Index Cond: ((order_id = o2.id) AND ("position" = 224))
                                             Heap Fetches: 0
                                             Index Searches: 0
               ->  Index Scan using pk_orders on orders o  (cost=0.42..8.44 rows=1 width=24) (actual time=0.023..0.023 rows=1.00 loops=1)
                     Index Cond: (id = o0.id)
                     Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                     Index Searches: 1
                     Buffers: shared hit=4
 Planning:
   Buffers: shared hit=357
 Planning Time: 1.600 ms
 Execution Time: 47.860 ms
```

## After

```
 Limit  (cost=764.90..764.90 rows=1 width=24) (actual time=5.590..5.592 rows=1.00 loops=1)
   Buffers: shared hit=364
   ->  Sort  (cost=764.90..764.90 rows=1 width=24) (actual time=5.590..5.591 rows=1.00 loops=1)
         Sort Key: o.scheduled_at DESC, o.id DESC
         Sort Method: quicksort  Memory: 25kB
         Buffers: shared hit=364
         ->  Nested Loop  (cost=739.97..764.89 rows=1 width=24) (actual time=5.561..5.563 rows=1.00 loops=1)
               Buffers: shared hit=358
               ->  Unique  (cost=739.55..739.56 rows=3 width=16) (actual time=5.556..5.557 rows=1.00 loops=1)
                     Buffers: shared hit=354
                     ->  Sort  (cost=739.55..739.55 rows=3 width=16) (actual time=5.554..5.555 rows=1.00 loops=1)
                           Sort Key: o0.id
                           Sort Method: quicksort  Memory: 25kB
                           Buffers: shared hit=354
                           ->  Append  (cost=81.94..739.52 rows=3 width=16) (actual time=5.535..5.548 rows=1.00 loops=1)
                                 Buffers: shared hit=354
                                 ->  Bitmap Heap Scan on orders o0  (cost=81.94..158.46 rows=1 width=16) (actual time=0.185..0.185 rows=0.00 loops=1)
                                       Recheck Cond: ((number)::text ~~* '%REF-0000224%'::text)
                                       Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                       Buffers: shared hit=24
                                       ->  Bitmap Index Scan on ix_orders_number_trgm  (cost=0.00..81.93 rows=20 width=0) (actual time=0.174..0.174 rows=0.00 loops=1)
                                             Index Cond: ((number)::text ~~* '%REF-0000224%'::text)
                                             Index Searches: 1
                                             Buffers: shared hit=24
                                 ->  Nested Loop  (cost=86.54..452.97 rows=1 width=16) (actual time=5.349..5.356 rows=1.00 loops=1)
                                       Buffers: shared hit=321
                                       ->  Bitmap Heap Scan on shipments s  (cost=86.12..199.77 rows=30 width=16) (actual time=5.335..5.341 rows=1.00 loops=1)
                                             Recheck Cond: ((ref_number)::text ~~* '%REF-0000224%'::text)
                                             Rows Removed by Index Recheck: 11
                                             Heap Blocks: exact=2
                                             Buffers: shared hit=317
                                             ->  Bitmap Index Scan on ix_shipments_ref_number_trgm  (cost=0.00..86.11 rows=30 width=0) (actual time=5.327..5.327 rows=12.00 loops=1)
                                                   Index Cond: ((ref_number)::text ~~* '%REF-0000224%'::text)
                                                   Index Searches: 1
                                                   Buffers: shared hit=315
                                       ->  Index Scan using pk_orders on orders o1  (cost=0.42..8.44 rows=1 width=16) (actual time=0.013..0.013 rows=1.00 loops=1)
                                             Index Cond: (id = s.order_id)
                                             Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                             Index Searches: 1
                                             Buffers: shared hit=4
                                 ->  Nested Loop  (cost=43.50..128.07 rows=1 width=16) (actual time=0.006..0.006 rows=0.00 loops=1)
                                       Buffers: shared hit=9
                                       ->  Bitmap Heap Scan on orders o2  (cost=43.08..119.61 rows=1 width=16) (actual time=0.006..0.006 rows=0.00 loops=1)
                                             Recheck Cond: ((number)::text ~~* 'REF'::text)
                                             Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                             Buffers: shared hit=9
                                             ->  Bitmap Index Scan on ix_orders_number_trgm  (cost=0.00..43.08 rows=20 width=0) (actual time=0.005..0.005 rows=0.00 loops=1)
                                                   Index Cond: ((number)::text ~~* 'REF'::text)
                                                   Index Searches: 1
                                                   Buffers: shared hit=9
                                       ->  Index Only Scan using ix_shipments_order_id_position on shipments s0  (cost=0.42..4.44 rows=1 width=16) (never executed)
                                             Index Cond: ((order_id = o2.id) AND ("position" = 224))
                                             Heap Fetches: 0
                                             Index Searches: 0
               ->  Index Scan using pk_orders on orders o  (cost=0.42..8.44 rows=1 width=24) (actual time=0.004..0.004 rows=1.00 loops=1)
                     Index Cond: (id = o0.id)
                     Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                     Index Searches: 1
                     Buffers: shared hit=4
 Planning:
   Buffers: shared hit=385
 Planning Time: 1.567 ms
 Execution Time: 5.752 ms
```

## What changed

Before, the Ref N branch had no index to use: a parallel sequential scan of all 300 000 shipments of
every company (5 376 buffers, 48 ms). After, a bitmap scan of the trigram index finds twelve candidates
and keeps one (317 buffers, 6 ms); both number branches use `ix_orders_number_trgm` throughout.
