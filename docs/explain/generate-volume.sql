-- Fills a migrated, unseeded database with a volume large enough for the planner to care: 200 000 orders in
-- 50 companies, one or three shipments each, with cargo lines, Operations and invoices — about half a minute on
-- a laptop. Deterministic: every value follows from the row number. Ids have no column default, so they come from
-- PostgreSQL 18's uuidv7().
--
--   docker exec -i freitty-explain psql -U postgres -v ON_ERROR_STOP=1 < docs/explain/generate-volume.sql
--
-- Every constraint of the schema holds (docs/steps/02-domain.md): enums in range, a Cross-Dock kind only on a
-- Cross-Dock, a carrier or a driver as the transport kind says, a transfer hub other than the hub, milestones that
-- match the status and are in order, a submitted order with its Scheduled date, destination and transport, unique
-- numbers; shipment positions from 1 with unique Ref Ns per order, arrival and dock columns all or none; one cargo
-- line per Unit Type; a trailer number exactly on Unloading and Loading.
--
-- Shape of order n (1 … 200 000): company n % 50; every 4th a Consolidation (3 shipments), the rest
-- Cross-Docks; status by n % 20 — 0 Draft, 1–2 Ready, 3–4 In progress, the rest Completed; Scheduled dates spread
-- over 2025–2026, none on every other draft.

BEGIN;

CREATE TEMP TABLE generated_companies AS
SELECT c AS company_no, uuidv7() AS id, uuidv7() AS admin_id, uuidv7() AS dispatcher_id
FROM generate_series(0, 49) AS c;

INSERT INTO companies (id, name, balance)
SELECT id, 'Volume company ' || company_no, 1000.00 FROM generated_companies;

INSERT INTO users (id, company_id, name, role)
SELECT admin_id, id, 'Admin ' || company_no, 1 FROM generated_companies
UNION ALL
SELECT dispatcher_id, id, 'Dispatcher ' || company_no, 2 FROM generated_companies;

CREATE TEMP TABLE generated_hubs AS
SELECT h AS hub_no, uuidv7() AS id FROM generate_series(0, 1) AS h;

INSERT INTO hubs (id, name, short_name, region, time_zone)
SELECT id, 'Volume hub ' || hub_no, 'VH' || hub_no, 'ON', 'America/Toronto' FROM generated_hubs;

CREATE TEMP TABLE generated_carriers AS
SELECT k AS carrier_no, uuidv7() AS id FROM generate_series(0, 2) AS k;

INSERT INTO carriers (id, name, phone)
SELECT id, 'Volume carrier ' || carrier_no, NULL FROM generated_carriers;

-- One row per order with everything derived from n; the instants are built from the Scheduled date.
CREATE TEMP TABLE generated_orders AS
SELECT
    n,
    uuidv7() AS id,
    n % 50 AS company_no,
    CASE WHEN n % 4 = 3 THEN 2 ELSE 1 END AS type,
    CASE WHEN n % 20 = 0 THEN 1 WHEN n % 20 IN (1, 2) THEN 2 WHEN n % 20 IN (3, 4) THEN 3 ELSE 4 END AS status,
    CASE WHEN n % 4 = 3 THEN 2 + n % 2 ELSE 1 END AS shipment_count,
    n % 2 AS hub_no,
    1 + n % 3 AS transport_kind,
    timestamptz '2025-01-01 13:00:00+00' + (n * 7919 % 700) * interval '1 day' + (n % 9) * interval '1 hour'
        AS scheduled_at
FROM generate_series(1, 200000) AS n;

INSERT INTO orders (
    id, number, type, cross_dock_kind, status, company_id, created_by_id, hub_id, transfer_hub_id,
    scheduled_at, destination_city, destination_region, transport_kind, carrier_id, driver_name,
    created_at, submitted_at, started_at, completed_at, updated_at, version)
SELECT
    o.id,
    CASE WHEN o.status = 1 THEN 'DRAFT-' ELSE 'FR' END || lpad(o.n::text, 7, '0'),
    o.type,
    CASE WHEN o.type = 1 THEN 1 + o.n % 2 END,
    o.status,
    c.id,
    CASE WHEN o.n % 3 = 0 THEN c.admin_id ELSE c.dispatcher_id END,
    h.id,
    CASE WHEN o.n % 10 = 5 THEN other_hub.id END,
    details.scheduled_at,
    details.destination_city,
    details.destination_region,
    details.transport_kind,
    CASE WHEN details.transport_kind = 1 THEN carrier.id END,
    CASE WHEN details.transport_kind = 2 THEN 'Driver ' || o.n % 40 END,
    o.scheduled_at - interval '4 days',
    milestones.submitted_at,
    milestones.started_at,
    milestones.completed_at,
    coalesce(milestones.completed_at, milestones.started_at, milestones.submitted_at, o.scheduled_at - interval '4 days'),
    1 + o.status * 3
FROM generated_orders AS o
JOIN generated_companies AS c ON c.company_no = o.company_no
JOIN generated_hubs AS h ON h.hub_no = o.hub_no
JOIN generated_hubs AS other_hub ON other_hub.hub_no = 1 - o.hub_no
JOIN generated_carriers AS carrier ON carrier.carrier_no = o.n % 3
CROSS JOIN LATERAL (
    -- Every other draft has filled nothing in yet.
    SELECT
        CASE WHEN o.status = 1 AND o.n % 40 = 0 THEN NULL ELSE o.scheduled_at END AS scheduled_at,
        CASE WHEN o.status = 1 AND o.n % 40 = 0 THEN NULL ELSE 'City ' || o.n % 25 END AS destination_city,
        CASE WHEN o.status = 1 AND o.n % 40 = 0 THEN NULL ELSE 'ON' END AS destination_region,
        CASE WHEN o.status = 1 AND o.n % 40 = 0 THEN NULL ELSE o.transport_kind END AS transport_kind
) AS details
CROSS JOIN LATERAL (
    SELECT
        CASE WHEN o.status > 1 THEN o.scheduled_at - interval '4 days' + interval '30 minutes' END AS submitted_at,
        CASE WHEN o.status > 2 THEN o.scheduled_at + interval '10 minutes' END AS started_at,
        CASE WHEN o.status = 4 THEN o.scheduled_at + interval '1 day' END AS completed_at
) AS milestones;

CREATE TEMP TABLE generated_shipments AS
SELECT
    uuidv7() AS id,
    o.id AS order_id,
    o.n,
    o.status,
    o.scheduled_at,
    position,
    row_number() OVER (ORDER BY o.n, position) AS shipment_no
FROM generated_orders AS o
CROSS JOIN LATERAL generate_series(1, o.shipment_count) AS position;

-- Shipments of an order the hub has started have arrived and have a dock; those of a completed order are
-- received.
INSERT INTO shipments (
    id, order_id, position, ref_number, trailer_type, truck_number, trailer_number, arrived_at, received_at,
    dock, bay, floor_lead, dock_assigned_at)
SELECT
    s.id,
    s.order_id,
    s.position,
    'REF-' || lpad(s.shipment_no::text, 7, '0'),
    CASE WHEN s.status > 2 THEN 1 + s.shipment_no % 3 END,
    CASE WHEN s.status > 2 THEN 'TRK-' || s.shipment_no END,
    CASE WHEN s.status > 2 THEN 'TRL-' || s.shipment_no END,
    CASE WHEN s.status > 2 THEN s.scheduled_at + interval '10 minutes' END,
    CASE WHEN s.status = 4 THEN s.scheduled_at + interval '1 hour' END,
    CASE WHEN s.status > 2 THEN (1 + s.shipment_no % 20)::text END,
    CASE WHEN s.status > 2 THEN 'A' END,
    CASE WHEN s.status > 2 THEN 'Floor lead' END,
    CASE WHEN s.status > 2 THEN s.scheduled_at + interval '12 minutes' END
FROM generated_shipments AS s;

-- Standard on every shipment, ExtraLarge on every fifth as well.
INSERT INTO cargo_lines (id, shipment_id, unit_type, quantity)
SELECT uuidv7(), s.id, 1, 5 + s.shipment_no % 20 FROM generated_shipments AS s
UNION ALL
SELECT uuidv7(), s.id, 2, 1 + s.shipment_no % 4 FROM generated_shipments AS s WHERE s.shipment_no % 5 = 0;

-- What was declared is unloaded once the hub has started, and loaded out of a completed order: no quantity
-- delta, and nothing left on stock where the order is completed.
INSERT INTO operations (
    id, shipment_id, type, unit_type, quantity, trailer_number, applied_at, is_billable, photo_count, comment_count)
SELECT uuidv7(), s.id, 1, c.unit_type, c.quantity, 'TRL-' || s.shipment_no,
    s.scheduled_at + interval '30 minutes', true, 1, 0
FROM generated_shipments AS s JOIN cargo_lines AS c ON c.shipment_id = s.id WHERE s.status > 2
UNION ALL
SELECT uuidv7(), s.id, 4, c.unit_type, c.quantity, 'OUT-' || s.n,
    s.scheduled_at + interval '20 hours', true, 0, 0
FROM generated_shipments AS s JOIN cargo_lines AS c ON c.shipment_id = s.id WHERE s.status = 4;

INSERT INTO invoices (id, number, order_id, amount, issued_at, paid_at)
SELECT uuidv7(), 'V' || lpad(o.n::text, 9, '0'), o.id, 100.00 + o.n % 900,
    o.scheduled_at + interval '1 day 1 hour',
    CASE WHEN o.n % 7 <> 0 THEN o.scheduled_at + interval '3 days' END
FROM generated_orders AS o WHERE o.status = 4;

COMMIT;

VACUUM ANALYZE;
