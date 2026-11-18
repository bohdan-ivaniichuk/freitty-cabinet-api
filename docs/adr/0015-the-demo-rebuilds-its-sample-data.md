# 0015 — The demo rebuilds its sample data

- Status: accepted
- Date: 2026-11-18

## Context

[ADR 0008](0008-sample-data-built-through-the-domain.md) dates the sample data from the anchor day,
the day before seeding, and refuses to refresh it: fresh data means an empty database. The
assignment is judged by its deployed demo, opened on whatever day a reviewer chooses. Two days
after seeding, the "Today" filter is empty and the dashboard is stale. And every operation a
visitor recorded through the order page stays there for the next visitor.

## Decision

Two settings, both off unless the environment turns them on: `Seed:Enabled`, as before, and
`Seed:Refresh`, which has an effect only with the first. With both on, as in `docker-compose.yml`
and the deployment, the API rebuilds the sample data at startup whatever the database holds, and
again while it runs once the anchor day has changed. A hosted service checks every 15 minutes on
the injected clock.

A rebuild is one transaction: one `TRUNCATE TABLE` naming every table of the EF Core model, then
the seed. A failure rolls back to the previous data, and no reader ever sees an empty database.
The migrations history is not in the model and survives.

Every row gets a new id, so a request whose statements fall on both sides of the commit would look
up ids that are gone. Measured before the gate existed: `/me` answered `500` because its user had
vanished, a list said 70 orders and showed no cards, and FR001383 was not found. So a request gate
holds the process still for a rebuild. Closing it waits — 10 seconds at most — for the requests in
flight, and requests that arrive meanwhile wait until it opens. A rebuild takes well under a
second. Behind the closed gate nothing of the process reads or writes, so the rebuild needs no
lock of its own beyond the one `TRUNCATE` takes.

This holds for one instance, which is what the deployment runs.

## Alternatives

- **Keep the reference data and rebuild only the orders** — the company, the users and the hubs
  keep their ids, so a request could not lose its current user and the gate would be unnecessary.
  Not taken: the seed would have to load the existing reference rows and build on them instead of
  building everything from nothing. That is a second way to seed, and it leaves rows a visitor
  could have changed.
- **Shift the dates of the existing rows in place** — one `UPDATE` per table moves every instant
  by the days that have passed, and the ids stay. Not taken: it does not undo what a visitor
  recorded, and it rewrites history by hand outside the aggregate, which ADR 0008 exists to avoid.
- **A scheduled job outside the application, or a nightly redeploy** — keeps the delete out of the
  API. Not taken: a second thing to build, schedule and deploy for one instance, as with the
  separate seeding command that ADR 0008 rejected.
- **Do nothing and reset by hand** — no code at all. Not taken: the demo is opened unannounced.

## Consequences

- The demo is dated from today, and whatever a visitor records lasts until the next day.
- The API pauses for a rebuild, once a day just after midnight in the hubs' zone. A request in
  flight finishes first, and a request that arrives during the rebuild waits for it.
- A page left open across a rebuild holds ids that are gone. Its Hub filter matches nothing, and
  the list is empty until the page is reloaded.
- One instance only. During a rolling deploy the new instance's startup rebuild runs beside the
  old instance. The old instance's readers block on the `TRUNCATE` lock for the length of the
  rebuild and then read the new rows. A request whose statements straddle that commit can still
  meet ids that are gone, and a deadlock between its lock order and the `TRUNCATE` can abort one
  of the two. A deploy is rare and brief; several instances would need the database itself to
  agree on the switch.
- The timer is tested without a hook in production code: a test moves the fake clock and asks
  through the API until it sees the result, within a bound that only ends a test that would hang.
