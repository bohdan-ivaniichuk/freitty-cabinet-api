# 0003 — Order is one aggregate, and the only rich one

- Status: accepted
- Date: 2026-11-17

## Context

The rules of this system sit in one place: an order. Whether freight can be loaded depends on what
was unloaded and disposed; an alert opens and closes with the quantities; a status moves with the
first action of the hub and stops at completion. Everything else — companies, hubs, carriers, the catalogs —
is data without rules. [ADR 0001](0001-light-clean-architecture.md) already keeps the ceremony low
and sends reads straight to SQL.

## Decision

Tactical DDD, applied where there are rules and nowhere else.

- `Order` is an aggregate root. Its Shipments, Cargo Lines, Operations, Alerts, Supply lines
  and selected services are created and changed only through methods of `Order`; their constructors
  and mutators are `internal` to the domain assembly.
- The aggregate holds ids of everything outside it — companies, users, hubs, carriers, services,
  supply items — and never assigns a navigation to them.
- The aggregate is loaded whole for a write and saved in one transaction. Every change sets
  `Order.UpdatedAt` and increments `Order.Version`, so a change to any part is a change to the
  root, and the concurrency token of the write flow belongs on the root row.
- `Invoice` is a separate aggregate with its own lifecycle and numbering. Companies, users, hubs,
  carriers, services and supply items are reference data: plain entities that only check their own
  values.
- Reads do not load the aggregate. They project from the tables.
- Not taken: a repository per aggregate, domain events with a dispatcher, domain services,
  specifications, strongly typed ids, a value object per field.

## Alternatives

- **Anemic entities with rules in handlers** — the least code now. Rejected: the seed and the write
  flow would each re-implement "is this allowed", and nothing would stop either from writing a
  completed order with freight still on stock.
- **Each shipment as its own aggregate, with the order as a thin root that holds the status** —
  two docks could work two shipments of one Consolidation without ever conflicting. Rejected: the
  rules that matter span shipments — an order starts with the first action on any of them and
  completes only when all are received, nothing is on stock and no alert is open — so they would
  need a transaction across aggregates anyway.
- **Supply lines or alerts as aggregates of their own** — fewer conflicts between concurrent
  changes. Rejected: their rules depend on the order's status and quantities, so the check would
  move outside the aggregate and need its own locking.
- **Full tactical DDD** (repositories, domain events, value objects throughout) — the read side
  would pay for abstractions it does not use, and the events would have no subscriber.

## Consequences

- An invalid order cannot be built by the seed, by a handler or by a test: they all go through the
  same methods.
- Two concurrent changes to one order conflict even when they touch different parts — an operation
  on one shipment and a supply line, or operations on two shipments. One of them retries. Accepted:
  an order sees a handful of writes an hour, and a retry after a conflict is cheap.
- Some arithmetic exists twice: in the aggregate, for its rules, and in SQL, for the screens. They
  are kept honest by tests on both sides, not by sharing code.
- `Order` keeps read-only navigations to reference data because projections need them. EF Core
  fills them when it reads; the domain neither assigns nor follows them, so saving an order can
  never insert a hub or a catalog row by accident.
- Domain events and an outbox can be added when something outside the aggregate needs to react —
  notifications, billing — without changing the model.
