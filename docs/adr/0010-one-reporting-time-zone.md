# 0010 — One reporting time zone, from configuration

- Status: accepted
- Date: 2026-11-18

## Context

Instants are stored in UTC ([step 2](../steps/02-domain.md)), and each hub knows its IANA zone, so
a single event is shown in the local time of the hub it happened at. But some questions are not
about one event: "orders scheduled today", "this week", the buckets of the activity chart. They
need a day to begin somewhere, and a company's orders can sit in hubs of different zones.

## Decision

The cabinet has one reporting time zone, read from configuration (`Cabinet:TimeZone`,
`America/Toronto`) and validated at startup: it must be an IANA id, as a hub's zone must, because
it is also handed to PostgreSQL. One class in the application layer, the reporting
calendar, turns "today", "this week" and "the last 30 days" into UTC ranges from `TimeProvider`
and that zone. The list filters by those ranges; the dashboard SQL receives the zone as a
parameter for `date_trunc`.

An instant on a card or a detail page is still shown in the zone of its hub.

## Alternatives

- **The zone of each order's hub** — "today" means today where the freight is. Correct per order,
  and the right meaning for a hub's own screens. Rejected here: the filter becomes an expression
  over a joined column that no index serves, and a chart has no single axis when two hubs
  disagree about where Monday starts.
- **A zone per company (a column)** — the client decides how its week is cut. The right model for
  a real product. Not taken now: a migration and a setting screen for a value that is the same for
  every seeded row. It is the next step, and it replaces only where the calendar gets its zone.
- **The viewer's zone, sent by the browser** — what many dashboards do. Rejected: two dispatchers
  of one company would see different totals for "today", and the numbers could not be reproduced
  from a link.
- **UTC** — nothing to configure. Rejected: in Toronto the day would turn at 19:00 or 20:00, so
  an order scheduled for Monday 21:00 would be filed under Tuesday while its card says Monday.

## Consequences

- Date filters are plain range predicates on a UTC column, served by an ordinary index.
- Every "today" in the application is the same today, and tests fix it with `FakeTimeProvider`.
- Both seeded hubs are in the reporting zone, so nothing on screen disagrees. With a hub in
  another zone, a card could show 23:30 on the 14th while the "Today" filter, cut in the reporting
  zone, already counts the 15th. Accepted until the zone moves to the company.
- A wrong zone id is a startup failure, not a wrong chart.
- Every step along a time axis has to be taken in the zone as well, not only the truncation: an
  axis stepped in UTC drifts by an hour at a daylight saving change. The dashboard SQL passes the
  zone to `generate_series` and `date_subtract` for that reason.
