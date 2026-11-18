# Search by order number

The page query of `GET /api/v1/orders?q=FR0000150`: the `UNION` of the number branch and the Ref N
branch (no label branch: the text has no hyphen). Dropped for "before": `ix_orders_number_trgm`.

The statement is the one EF Core sends, copied from the SQL log of `dotnet run` with the parameters
replaced by literals for "Volume company 0" (4 000 of the 200 000 orders). How the data was made and
how to repeat the measurement: [README](README.md).

```sql
SELECT o.id
FROM orders AS o
WHERE o.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND o.id IN (
    SELECT o0.id
    FROM orders AS o0
    WHERE o0.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND o0.number ILIKE '%FR0000150%' ESCAPE '\'
    UNION
    SELECT s.order_id AS id
    FROM orders AS o1
    INNER JOIN shipments AS s ON o1.id = s.order_id
    WHERE o1.company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe' AND s.ref_number ILIKE '%FR0000150%' ESCAPE '\'
)
ORDER BY o.scheduled_at DESC, o.id DESC
LIMIT 12 OFFSET 0
```

## Before — without `ix_orders_number_trgm`

Measured as `BEGIN; DROP INDEX ix_orders_number_trgm; EXPLAIN (ANALYZE, BUFFERS) …; ROLLBACK;`.

```
 Limit  (cost=5659.25..5659.25 rows=1 width=24) (actual time=5.099..5.101 rows=1.00 loops=1)
   Buffers: shared hit=2414
   ->  Sort  (cost=5659.25..5659.25 rows=1 width=24) (actual time=5.098..5.099 rows=1.00 loops=1)
         Sort Key: o.scheduled_at DESC, o.id DESC
         Sort Method: quicksort  Memory: 25kB
         Buffers: shared hit=2414
         ->  Nested Loop  (cost=5642.76..5659.24 rows=1 width=24) (actual time=5.015..5.016 rows=1.00 loops=1)
               Buffers: shared hit=2408
               ->  Unique  (cost=5642.34..5642.35 rows=2 width=16) (actual time=4.975..4.976 rows=1.00 loops=1)
                     Buffers: shared hit=2404
                     ->  Sort  (cost=5642.34..5642.35 rows=2 width=16) (actual time=4.974..4.975 rows=1.00 loops=1)
                           Sort Key: o0.id
                           Sort Method: quicksort  Memory: 25kB
                           Buffers: shared hit=2404
                           ->  Append  (cost=158.37..5642.33 rows=2 width=16) (actual time=1.308..4.949 rows=1.00 loops=1)
                                 Buffers: shared hit=2404
                                 ->  Bitmap Heap Scan on orders o0  (cost=158.37..5210.84 rows=1 width=16) (actual time=1.307..4.778 rows=1.00 loops=1)
                                       Recheck Cond: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                       Filter: ((number)::text ~~* '%FR0000150%'::text)
                                       Rows Removed by Filter: 3999
                                       Heap Blocks: exact=2353
                                       Buffers: shared hit=2385
                                       ->  Bitmap Index Scan on ix_orders_company_id_number  (cost=0.00..158.37 rows=3993 width=0) (actual time=0.609..0.609 rows=4000.00 loops=1)
                                             Index Cond: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                             Index Searches: 1
                                             Buffers: shared hit=32
                                 ->  Nested Loop  (cost=65.05..431.48 rows=1 width=16) (actual time=0.169..0.169 rows=0.00 loops=1)
                                       Buffers: shared hit=19
                                       ->  Bitmap Heap Scan on shipments s  (cost=64.63..178.28 rows=30 width=16) (actual time=0.169..0.169 rows=0.00 loops=1)
                                             Recheck Cond: ((ref_number)::text ~~* '%FR0000150%'::text)
                                             Buffers: shared hit=19
                                             ->  Bitmap Index Scan on ix_shipments_ref_number_trgm  (cost=0.00..64.62 rows=30 width=0) (actual time=0.155..0.155 rows=0.00 loops=1)
                                                   Index Cond: ((ref_number)::text ~~* '%FR0000150%'::text)
                                                   Index Searches: 1
                                                   Buffers: shared hit=19
                                       ->  Index Scan using pk_orders on orders o1  (cost=0.42..8.44 rows=1 width=16) (never executed)
                                             Index Cond: (id = s.order_id)
                                             Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                             Index Searches: 0
               ->  Index Scan using pk_orders on orders o  (cost=0.42..8.44 rows=1 width=24) (actual time=0.037..0.037 rows=1.00 loops=1)
                     Index Cond: (id = o0.id)
                     Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                     Index Searches: 1
                     Buffers: shared hit=4
 Planning:
   Buffers: shared hit=337
 Planning Time: 2.070 ms
 Execution Time: 5.340 ms
```

## After

```
 Limit  (cost=585.38..585.39 rows=1 width=24) (actual time=2.349..2.351 rows=1.00 loops=1)
   Buffers: shared hit=150
   ->  Sort  (cost=585.38..585.39 rows=1 width=24) (actual time=2.348..2.349 rows=1.00 loops=1)
         Sort Key: o.scheduled_at DESC, o.id DESC
         Sort Method: quicksort  Memory: 25kB
         Buffers: shared hit=150
         ->  Nested Loop  (cost=568.90..585.37 rows=1 width=24) (actual time=2.315..2.316 rows=1.00 loops=1)
               Buffers: shared hit=144
               ->  Unique  (cost=568.48..568.49 rows=2 width=16) (actual time=2.291..2.292 rows=1.00 loops=1)
                     Buffers: shared hit=140
                     ->  Sort  (cost=568.48..568.48 rows=2 width=16) (actual time=2.286..2.287 rows=1.00 loops=1)
                           Sort Key: o0.id
                           Sort Method: quicksort  Memory: 25kB
                           Buffers: shared hit=140
                           ->  Append  (cost=60.45..568.47 rows=2 width=16) (actual time=2.129..2.279 rows=1.00 loops=1)
                                 Buffers: shared hit=140
                                 ->  Bitmap Heap Scan on orders o0  (cost=60.45..136.97 rows=1 width=16) (actual time=2.129..2.142 rows=1.00 loops=1)
                                       Recheck Cond: ((number)::text ~~* '%FR0000150%'::text)
                                       Rows Removed by Index Recheck: 9
                                       Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                       Heap Blocks: exact=8
                                       Buffers: shared hit=121
                                       ->  Bitmap Index Scan on ix_orders_number_trgm  (cost=0.00..60.45 rows=20 width=0) (actual time=2.103..2.103 rows=10.00 loops=1)
                                             Index Cond: ((number)::text ~~* '%FR0000150%'::text)
                                             Index Searches: 1
                                             Buffers: shared hit=113
                                 ->  Nested Loop  (cost=65.05..431.48 rows=1 width=16) (actual time=0.135..0.136 rows=0.00 loops=1)
                                       Buffers: shared hit=19
                                       ->  Bitmap Heap Scan on shipments s  (cost=64.63..178.28 rows=30 width=16) (actual time=0.135..0.135 rows=0.00 loops=1)
                                             Recheck Cond: ((ref_number)::text ~~* '%FR0000150%'::text)
                                             Buffers: shared hit=19
                                             ->  Bitmap Index Scan on ix_shipments_ref_number_trgm  (cost=0.00..64.62 rows=30 width=0) (actual time=0.133..0.133 rows=0.00 loops=1)
                                                   Index Cond: ((ref_number)::text ~~* '%FR0000150%'::text)
                                                   Index Searches: 1
                                                   Buffers: shared hit=19
                                       ->  Index Scan using pk_orders on orders o1  (cost=0.42..8.44 rows=1 width=16) (never executed)
                                             Index Cond: (id = s.order_id)
                                             Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                                             Index Searches: 0
               ->  Index Scan using pk_orders on orders o  (cost=0.42..8.44 rows=1 width=24) (actual time=0.021..0.021 rows=1.00 loops=1)
                     Index Cond: (id = o0.id)
                     Filter: (company_id = '01a1ec12-b046-79b9-9bbd-4b3fc38576fe'::uuid)
                     Index Searches: 1
                     Buffers: shared hit=4
 Planning:
   Buffers: shared hit=365
 Planning Time: 1.704 ms
 Execution Time: 2.546 ms
```

## What changed

Before, the number branch read every order of the company through the company index and filtered
3 999 of them out (2 385 buffers). After, it is a bitmap scan of the trigram index that finds ten
candidates and keeps one (121 buffers); the Ref N branch uses its own trigram index both times.
