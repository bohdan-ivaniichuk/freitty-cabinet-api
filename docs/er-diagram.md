# ER diagram

The schema created by the `InitialSchema` migration
([step 2](steps/02-domain.md)), with the indexes of `OrderListIndexes`
([step 4](steps/04-order-list.md)) and the `photos` table of `OperationPhotos`
([step 12](steps/12-photos.md)). The bytes of a photo are not in the database: `storage_key` names
a blob of the private container `photos`
([ADR 0017](adr/0017-photos-in-blob-storage-behind-the-api.md)). Terms are defined in [`CONTEXT.md`](../CONTEXT.md).

Every `id` is a UUID v7 created by the domain. Instants are `timestamptz` in UTC. Enums are stored
as integers, each with a `CHECK` that lists the values of its enum.

## The `Order` aggregate

Everything in this diagram is loaded, changed and saved together, through `Order` only. A draft may
have no shipment yet; a submitted order has at least one.

```mermaid
erDiagram
    orders ||--o{ shipments : "has shipments"
    shipments ||--o{ cargo_lines : "declares"
    shipments ||--o{ operations : "ledger"
    shipments ||--o{ alerts : "raises"
    operations ||--o{ photos : "documented by"
    orders ||--o{ supply_lines : "buys"
    orders ||--o{ order_services : "includes"

    orders {
        uuid id PK
        varchar number "UK with company_id; UK alone once submitted; DRAFT-003, then FR001684"
        int type "CrossDock | Consolidation"
        int cross_dock_kind "nullable; Storage | Pickup"
        int status "Draft | Ready | InProgress | Completed"
        uuid company_id FK
        uuid created_by_id FK
        uuid hub_id FK
        uuid transfer_hub_id FK "nullable; hub-to-hub transfer"
        timestamptz scheduled_at "nullable in a draft"
        varchar destination_city "nullable in a draft"
        varchar destination_region "nullable in a draft"
        int transport_kind "nullable in a draft; Carrier | OwnDriver | SelfPickup"
        uuid carrier_id FK "set exactly for Carrier"
        varchar driver_name "set exactly for OwnDriver"
        timestamptz created_at
        timestamptz submitted_at "set exactly when not Draft"
        timestamptz started_at "set exactly when InProgress or Completed"
        timestamptz completed_at "set exactly when Completed; milestones are in order"
        timestamptz updated_at "set by every change to the aggregate"
        int version "incremented by every change to the aggregate; the optimistic concurrency token"
    }

    shipments {
        uuid id PK
        uuid order_id FK
        int position "UK with order_id; FR001676-1"
        varchar ref_number "UK with order_id; the client's Ref N"
        varchar warehouse_note "nullable"
        int trailer_type "arrival; nullable"
        varchar truck_number "arrival; nullable"
        varchar trailer_number "arrival; nullable"
        timestamptz arrived_at "arrival; nullable; the four are set together"
        timestamptz received_at "nullable; the hub finished receiving this shipment"
        varchar dock "dock; nullable"
        varchar bay "dock; nullable"
        varchar floor_lead "dock; nullable; hub staff, not a user"
        timestamptz dock_assigned_at "dock; nullable; the four are set together"
    }

    cargo_lines {
        uuid id PK
        uuid shipment_id FK
        int unit_type "UK with shipment_id"
        int quantity "declared; > 0"
    }

    operations {
        uuid id PK
        uuid shipment_id FK
        int type "Unloading | Disposal | Restack | Loading"
        int unit_type
        int quantity "> 0"
        varchar trailer_number "set exactly for Unloading and Loading"
        timestamptz applied_at "when it was recorded"
        boolean is_billable
        int photo_count "the number of its photos, kept so by the aggregate"
        int comment_count
    }

    photos {
        uuid id PK
        uuid operation_id FK "at most ten per operation"
        varchar storage_key "uploads/<a UUID of its own> or samples/<name>"
        varchar content_type "found in the bytes: image/jpeg | image/png | image/webp"
        bigint size_bytes "> 0"
        timestamptz uploaded_at
    }

    alerts {
        uuid id PK
        uuid shipment_id FK
        int type "QuantityDelta | MissingPhoto"
        timestamptz raised_at
        timestamptz resolved_at "nullable; one open per shipment and type"
    }

    supply_lines {
        uuid id PK
        uuid order_id FK
        uuid supply_item_id FK
        int quantity "> 0"
        numeric unit_price "snapshot of the catalog price"
    }

    order_services {
        uuid id PK
        uuid order_id FK "UK with service_id"
        uuid service_id FK
    }
```

What the diagram does not show because it is not stored:

| Shown on screen | Computed from |
|---|---|
| Declared quantity of a shipment or an order | `cargo_lines` |
| Actual quantity, Quantity delta | `operations` of type Unloading against `cargo_lines`; a shortage counts once `received_at` is set |
| On stock | `operations`: unloaded − disposed − loaded |
| "Trailers: 2 consolidated" | distinct `trailer_number` of Loading `operations` |
| An order "has an alert" | an `alerts` row with `resolved_at IS NULL` on any of its shipments |
| Supply line total and subtotal | `supply_lines.quantity × unit_price` |
| Photo and comment counters in the toolbar | sums of `photo_count` and `comment_count` over `operations` |

## The order and what it refers to

```mermaid
erDiagram
    companies ||--o{ users : "employs"
    companies ||--o{ orders : "owns"
    users ||--o{ orders : "created by"
    hubs ||--o{ orders : "handled at"
    hubs |o--o{ orders : "transferred to"
    carriers |o--o{ orders : "carried by"
    orders ||--o{ invoices : "billed by"
    services ||--o{ order_services : "selected in"
    supply_items ||--o{ supply_lines : "sold as"

    companies {
        uuid id PK
        varchar name
        numeric balance "kept for display; nothing adds up to it"
    }

    users {
        uuid id PK
        uuid company_id FK
        varchar name
        int role "Admin | Dispatcher"
    }

    hubs {
        uuid id PK
        varchar name UK
        varchar short_name "Mark, Tor"
        varchar region "ON"
        varchar time_zone "IANA id, e.g. America/Toronto"
    }

    carriers {
        uuid id PK
        varchar name UK
        varchar phone "nullable"
    }

    services {
        uuid id PK
        varchar name UK
    }

    supply_items {
        uuid id PK
        varchar sku UK
        varchar category
        numeric unit_price
    }

    invoices {
        uuid id PK
        varchar number UK
        uuid order_id FK "the paying company is the order's"
        numeric amount "fixed when issued"
        timestamptz issued_at
        timestamptz paid_at "nullable"
    }

    orders {
        uuid id PK
    }

    order_services {
        uuid id PK
    }

    supply_lines {
        uuid id PK
    }
```

## Delete behaviour

- `CASCADE` for rows that only describe an order: `shipments`, `supply_lines`,
  `order_services` follow their `orders` row; `cargo_lines` follow their `shipments` row; `photos`
  follow their `operations` row. Their blobs do not: a rebuild of the sample data deletes every
  uploaded one.
- `RESTRICT` for the ledger: a `shipments` row that has `operations` or `alerts` cannot be
  deleted, and so neither can its order. A draft has neither, so a draft can be deleted.
- `RESTRICT` everywhere else: a company, user, hub, carrier, service or supply item that an order
  refers to cannot be deleted, and neither can an order that has an invoice.

## Indexes

Besides the primary keys, the unique keys named in the diagrams and an index per foreign key,
`alerts` has a partial unique index: `ix_alerts_shipment_id_type` on `(shipment_id, type) WHERE
resolved_at IS NULL`, one open Alert per shipment and type. Its name is matched to turn a lost race
to raise the same Alert into `409`
([ADR 0013](adr/0013-optimistic-concurrency-on-order-version.md)).

The Order List has three of its own (migration `OrderListIndexes`, which also enables `pg_trgm`):

| Index | On | Serves |
|---|---|---|
| `ix_orders_number_trgm` | `orders (number)`, GIN, `gin_trgm_ops` | the search's `number ILIKE '%…%'` |
| `ix_shipments_ref_number_trgm` | `shipments (ref_number)`, GIN, `gin_trgm_ops` | the search's `ref_number ILIKE '%…%'` |
| `ix_orders_company_id_scheduled_at_id` | `orders (company_id, scheduled_at DESC, id DESC)` | the default list of a company, newest Scheduled date first, without a sort |

The Pipeline board has one more (migration `PipelineIndex`):

| Index | On | Serves |
|---|---|---|
| `ix_orders_company_id_status_scheduled_at_id` | `orders (company_id, status, scheduled_at DESC, id DESC)` | the board's totals per status and its pick of the first orders of each, as index-only scans; the list filtered by status |

Official order numbers come from the sequence `order_number_seq` (migration
`OrderNumberSequence`, [ADR 0016](adr/0016-official-order-numbers-from-a-sequence.md)).

The query plans, before and after, are in [`explain/`](explain/README.md).
