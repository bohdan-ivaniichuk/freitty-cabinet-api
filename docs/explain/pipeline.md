# The pipeline board

The two statements of `GET /api/v1/orders/pipeline` that read the orders themselves, without
filters: the total of each status, and the ids of the first five orders of each status in the
list's order. The cards of those ids are then read by the card reader of the list, by id, and are
not measured here. Added for "after": `ix_orders_company_id_status_scheduled_at_id`, migration
`PipelineIndex`.

The statements are the ones EF Core sends, copied from the SQL log of `dotnet run` with the
parameters replaced by literals for "Volume company 0" (4 000 of the 200 000 orders). How the data
was made and how to repeat the measurement: [README](README.md). The generator gives each company
orders of two statuses — company 0 has 2 000 drafts and 2 000 completed orders — so two of the
four columns are empty; the plans do not depend on how many statuses there are. Both runs are on a
freshly `VACUUM ANALYZE`d table.

## The totals

```sql
SELECT o.status AS "Status", count(*)::int AS "Total"
FROM orders AS o
WHERE o.company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'
GROUP BY o.status
```

### Before

```
 HashAggregate  (cost=5207.77..5207.82 rows=4 width=8) (actual time=6.051..6.052 rows=2.00 loops=1)
   Group Key: status
   Batches: 1  Memory Usage: 32kB
   Buffers: shared hit=2385
   ->  Bitmap Heap Scan on orders o  (cost=159.06..5188.00 rows=3953 width=4) (actual time=0.608..5.657 rows=4000.00 loops=1)
         Recheck Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
         Heap Blocks: exact=2353
         Buffers: shared hit=2385
         ->  Bitmap Index Scan on ix_orders_company_id_number  (cost=0.00..158.07 rows=3953 width=0) (actual time=0.400..0.400 rows=4000.00 loops=1)
               Index Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
               Index Searches: 1
               Buffers: shared hit=32
 Planning:
   Buffers: shared hit=245
 Planning Time: 0.776 ms
 Execution Time: 6.471 ms
```

### After

```
 GroupAggregate  (cost=0.42..227.21 rows=4 width=8) (actual time=0.313..0.605 rows=2.00 loops=1)
   Group Key: status
   Buffers: shared hit=36
   ->  Index Only Scan using ix_orders_company_id_status_scheduled_at_id on orders o  (cost=0.42..207.00 rows=4033 width=4) (actual time=0.020..0.324 rows=4000.00 loops=1)
         Index Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
         Heap Fetches: 0
         Index Searches: 1
         Buffers: shared hit=36
 Planning:
   Buffers: shared hit=271
 Planning Time: 0.802 ms
 Execution Time: 0.657 ms
```

## The first cards of each status

EF Core translates the correlated `Take` into `ROW_NUMBER()` over a partition per status and joins
it to the statuses present; a `GroupBy` followed by `Take` is not translated at all.

```sql
SELECT o3.id
FROM (
    SELECT DISTINCT o.status
    FROM orders AS o
    WHERE o.company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'
) AS o2
INNER JOIN (
    SELECT o1.id, o1.scheduled_at, o1.status
    FROM (
        SELECT o0.id, o0.scheduled_at, o0.status, ROW_NUMBER() OVER(PARTITION BY o0.status ORDER BY o0.scheduled_at DESC, o0.id DESC) AS row
        FROM orders AS o0
        WHERE o0.company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'
    ) AS o1
    WHERE o1.row <= 5
) AS o3 ON o2.status = o3.status
ORDER BY o3.scheduled_at DESC, o3.id DESC
```

### Before

```
 Sort  (cost=11008.15..11018.04 rows=3953 width=24) (actual time=7.258..7.260 rows=10.00 loops=1)
   Sort Key: o0.scheduled_at DESC, o0.id DESC
   Sort Method: quicksort  Memory: 25kB
   Buffers: shared hit=4779
   ->  Hash Join  (cost=10622.17..10771.99 rows=3953 width=24) (actual time=6.864..7.234 rows=10.00 loops=1)
         Hash Cond: (o0.status = o.status)
         Buffers: shared hit=4773
         ->  WindowAgg  (cost=5424.19..5513.11 rows=3953 width=36) (actual time=2.898..3.266 rows=10.00 loops=1)
               Window: w1 AS (PARTITION BY o0.status ORDER BY o0.scheduled_at, o0.id ROWS UNBOUNDED PRECEDING)
               Run Condition: (row_number() OVER w1 <= 5)
               Storage: Memory  Maximum Storage: 17kB
               Buffers: shared hit=2388
               ->  Sort  (cost=5424.17..5434.05 rows=3953 width=28) (actual time=2.891..3.077 rows=4000.00 loops=1)
                     Sort Key: o0.status, o0.scheduled_at DESC, o0.id DESC
                     Sort Method: quicksort  Memory: 276kB
                     Buffers: shared hit=2388
                     ->  Bitmap Heap Scan on orders o0  (cost=159.06..5188.00 rows=3953 width=28) (actual time=0.500..1.505 rows=4000.00 loops=1)
                           Recheck Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
                           Heap Blocks: exact=2353
                           Buffers: shared hit=2385
                           ->  Bitmap Index Scan on ix_orders_company_id_number  (cost=0.00..158.07 rows=3953 width=0) (actual time=0.314..0.314 rows=4000.00 loops=1)
                                 Index Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
                                 Index Searches: 1
                                 Buffers: shared hit=32
         ->  Hash  (cost=5197.93..5197.93 rows=4 width=4) (actual time=3.950..3.950 rows=2.00 loops=1)
               Buckets: 1024  Batches: 1  Memory Usage: 9kB
               Buffers: shared hit=2385
               ->  HashAggregate  (cost=5197.89..5197.93 rows=4 width=4) (actual time=3.941..3.941 rows=2.00 loops=1)
                     Group Key: o.status
                     Batches: 1  Memory Usage: 32kB
                     Buffers: shared hit=2385
                     ->  Bitmap Heap Scan on orders o  (cost=159.06..5188.00 rows=3953 width=4) (actual time=0.599..3.569 rows=4000.00 loops=1)
                           Recheck Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
                           Heap Blocks: exact=2353
                           Buffers: shared hit=2385
                           ->  Bitmap Index Scan on ix_orders_company_id_number  (cost=0.00..158.07 rows=3953 width=0) (actual time=0.399..0.399 rows=4000.00 loops=1)
                                 Index Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
                                 Index Searches: 1
                                 Buffers: shared hit=32
 Planning:
   Buffers: shared hit=245
 Planning Time: 0.926 ms
 Execution Time: 7.413 ms
```

### After

```
 Sort  (cost=808.45..818.53 rows=4033 width=24) (actual time=0.972..0.974 rows=10.00 loops=1)
   Sort Key: o0.scheduled_at DESC, o0.id DESC
   Sort Method: quicksort  Memory: 25kB
   Buffers: shared hit=78
   ->  Hash Join  (cost=217.62..566.92 rows=4033 width=24) (actual time=0.503..0.941 rows=10.00 loops=1)
         Hash Cond: (o0.status = o.status)
         Buffers: shared hit=72
         ->  WindowAgg  (cost=0.49..287.66 rows=4033 width=36) (actual time=0.027..0.463 rows=10.00 loops=1)
               Window: w1 AS (PARTITION BY o0.status ORDER BY o0.scheduled_at, o0.id ROWS UNBOUNDED PRECEDING)
               Run Condition: (row_number() OVER w1 <= 5)
               Storage: Memory  Maximum Storage: 17kB
               Buffers: shared hit=36
               ->  Index Only Scan using ix_orders_company_id_status_scheduled_at_id on orders o0  (cost=0.42..207.00 rows=4033 width=28) (actual time=0.021..0.303 rows=4000.00 loops=1)
                     Index Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
                     Heap Fetches: 0
                     Index Searches: 1
                     Buffers: shared hit=36
         ->  Hash  (cost=217.08..217.08 rows=4 width=4) (actual time=0.469..0.469 rows=2.00 loops=1)
               Buckets: 1024  Batches: 1  Memory Usage: 9kB
               Buffers: shared hit=36
               ->  Unique  (cost=0.42..217.08 rows=4 width=4) (actual time=0.009..0.466 rows=2.00 loops=1)
                     Buffers: shared hit=36
                     ->  Index Only Scan using ix_orders_company_id_status_scheduled_at_id on orders o  (cost=0.42..207.00 rows=4033 width=4) (actual time=0.009..0.326 rows=4000.00 loops=1)
                           Index Cond: (company_id = '01a1efb3-2207-7a84-ad58-229898b8e6cc'::uuid)
                           Heap Fetches: 0
                           Index Searches: 1
                           Buffers: shared hit=36
 Planning:
   Buffers: shared hit=271
 Planning Time: 0.882 ms
 Execution Time: 1.044 ms
```

## What the plans show

Before, both statements read every order of the company from the heap: the totals once (2 385
buffers), the pick twice — once for the window and once for the `DISTINCT` statuses it is joined
to (4 779 buffers) — and the window sorted all 4 000 rows by status, date and id before numbering
them. The existing `ix_orders_company_id_scheduled_at_id` cannot help: its order starts with the
date, not the status.

After, every read is an index-only scan of the new index, 36 buffers each, with no heap fetch. The
totals become a `GroupAggregate` over rows already grouped, and the window gets its rows in its
own order, so its `Sort` is gone and the run condition stops it after five rows per status: 6.5 ms
to 0.66 ms and 7.4 ms to 1.0 ms. What remains is a sort of the ten picked rows and the `DISTINCT`
join, which reads the index a second time (36 buffers) and changes nothing in the result; it is
the price of the LINQ form.

The same index serves the list filtered by status: `status=Completed` on the first page reads 4
buffers with it and 1 024 through `ix_orders_company_id_scheduled_at_id`, which walks the dates
and discards the other statuses.

## Conclusion

The index is warranted: the board reads the whole company twice, and the index turns that into
small index-only scans whose cost grows with the index entries of the company rather than its heap
pages. Its cost is the write side — every insert of an order and every change of its status
(submit, start, complete) updates one more index entry — which for an order that changes status
three times in its life is small. Index-only scans need the visibility map to be current; on a busy
table some heap fetches return between vacuums, and the plan stays an index scan in the right
order.
