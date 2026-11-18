namespace Freitty.Infrastructure.Dashboard;

// The statements of the dashboard reader. Every enum value, the current instant and the reporting zone are
// parameters. Dapper fills a positional record through its constructor, matching each column by name, type and
// position: the aliases, the casts and the order of the columns are load-bearing.
internal static class DashboardSql
{
    // The tiles and the chart summary. The 30-day windows are rolling; the week starts on Monday in the zone.
    public const string Figures = """
        WITH this_week AS (
            SELECT date_trunc('week', @Now, @Zone) AS week_start
        ),
        order_figures AS (
            SELECT
                count(*) FILTER (WHERE status = ANY (@ActiveStatuses)) AS active,
                count(*) FILTER (
                    WHERE submitted_at < week_start
                      AND (completed_at IS NULL OR completed_at >= week_start)
                ) AS active_at_week_start,
                count(*) FILTER (
                    WHERE completed_at >= @Last30DaysFrom AND completed_at < @Now
                ) AS completed_last,
                count(*) FILTER (
                    WHERE completed_at >= @Previous30DaysFrom AND completed_at < @Last30DaysFrom
                ) AS completed_previous
            FROM orders
            CROSS JOIN this_week
            WHERE company_id = @CompanyId
        ),
        spend_figures AS (
            SELECT
                coalesce(sum(invoices.amount), 0.00) AS spend,
                count(DISTINCT invoices.order_id) AS invoiced_orders
            FROM invoices
            JOIN orders ON orders.id = invoices.order_id
            WHERE orders.company_id = @CompanyId
              AND invoices.issued_at >= @Last30DaysFrom AND invoices.issued_at < @Now
        )
        SELECT
            active::int AS "ActiveOrders",
            (active - active_at_week_start)::int AS "ActiveChangeThisWeek",
            completed_last::int AS "CompletedLast30Days",
            completed_previous::int AS "CompletedPrevious30Days",
            round((completed_last - completed_previous) * 100.0 / nullif(completed_previous, 0))::int
                AS "CompletedChangePercent",
            spend AS "SpendLast30Days",
            round(spend / nullif(invoiced_orders, 0), 2) AS "AverageSpendPerOrder"
        FROM order_figures
        CROSS JOIN spend_figures
        """;

    // The chart: the axis is generated and the data is joined to it, so an empty bucket is a row with zeros. Orders
    // count by completion, spend by the issue of the invoice. The axis is stepped on the local wall clock and turned
    // into instants once: a step taken on instants drifts by the hour a daylight saving change adds or removes, and
    // in a zone whose clocks skip midnight every start after the skip would stay at 01:00. Either way the starts
    // would stop matching what date_trunc returns for the data.
    public const string Activity = """
        WITH axis AS (
            SELECT local_start, local_start AT TIME ZONE @Zone AS bucket_start
            FROM generate_series(
                date_trunc(@Unit, @Now AT TIME ZONE @Zone) - @Step::interval * (@Buckets - 1),
                date_trunc(@Unit, @Now AT TIME ZONE @Zone),
                @Step::interval
            ) AS local_start
        ),
        completions AS (
            SELECT date_trunc(@Unit, completed_at, @Zone) AS bucket_start, count(*) AS completed_orders
            FROM orders
            WHERE company_id = @CompanyId
              AND completed_at >= (SELECT min(bucket_start) FROM axis)
            GROUP BY 1
        ),
        spending AS (
            SELECT date_trunc(@Unit, invoices.issued_at, @Zone) AS bucket_start, sum(invoices.amount) AS spend
            FROM invoices
            JOIN orders ON orders.id = invoices.order_id
            WHERE orders.company_id = @CompanyId
              AND invoices.issued_at >= (SELECT min(bucket_start) FROM axis)
            GROUP BY 1
        )
        SELECT
            axis.local_start::date AS "Start",
            coalesce(completions.completed_orders, 0)::int AS "CompletedOrders",
            coalesce(spending.spend, 0.00) AS "Spend"
        FROM axis
        LEFT JOIN completions USING (bucket_start)
        LEFT JOIN spending USING (bucket_start)
        ORDER BY axis.bucket_start
        """;

    // Awaiting action is what only the client can move: a draft, or an invoice it has not paid. A draft has no
    // invoice, so the two never meet in one order.
    public const string AttentionCounts = """
        SELECT
            count(*) FILTER (
                WHERE orders.status = @DraftStatus
                   OR EXISTS (
                       SELECT 1 FROM invoices
                       WHERE invoices.order_id = orders.id AND invoices.paid_at IS NULL)
            )::int AS "AwaitingAction",
            count(*) FILTER (
                WHERE EXISTS (
                    SELECT 1 FROM shipments
                    JOIN alerts ON alerts.shipment_id = shipments.id
                    WHERE shipments.order_id = orders.id AND alerts.resolved_at IS NULL)
            )::int AS "Alerts"
        FROM orders
        WHERE orders.company_id = @CompanyId
        """;

    // The orders with an open Alert, the most recently alerted first; the types as the card lists them.
    public const string AlertOrders = """
        SELECT
            orders.number AS "Number",
            array_agg(DISTINCT alerts.type ORDER BY alerts.type) AS "AlertTypes"
        FROM orders
        JOIN shipments ON shipments.order_id = orders.id
        JOIN alerts ON alerts.shipment_id = shipments.id
        WHERE orders.company_id = @CompanyId
          AND alerts.resolved_at IS NULL
        GROUP BY orders.id, orders.number
        ORDER BY max(alerts.raised_at) DESC, orders.number
        LIMIT @Limit
        """;
}
