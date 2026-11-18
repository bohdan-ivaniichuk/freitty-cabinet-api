# 10 — Docs and presentation

> **Draft, not groomed.** Written ahead of the grooming session so it does not start from zero.
> Everything under *Decisions* is a proposal; *Open questions* is what the session has to settle.
> Nothing here is input for implementation yet.

## Goal

What the reviewers read and hear is complete and true to the code: a README that gets a stranger to
a running app, the decision records, the schema, the query plans, and a presentation of ten to
fifteen minutes.

## Decisions

- **Nothing is written here that could have been written with the code.** ADRs, the ER diagram, the
  glossary and the query plans are produced by the steps that made the decisions. This step checks
  them against the final code and fills what is left.
- **The presentation follows the plan's outline** (§9): live demo, domain model, architecture,
  database, CI/CD and tests, the AI workflow, a walk through three or four files.
- **Every claim in the presentation points at something that can be opened**: an ADR, a test, a
  query plan, a line of code.
- **The mockup's inconsistencies are part of the story**: what was wrong in the sample data, how it
  was normalised, and what is deliberately not reproduced.

## Contract

| Document | State after this step |
|---|---|
| `README.md` | What it is, the public URL, run locally, tests, migrations, deployment, where the decisions are |
| `docs/adr/` | Every decision a reviewer could question has a record; none describes something the code no longer does |
| `docs/er-diagram.md` | Matches the migrations |
| `CONTEXT.md` | Matches the names in the code |
| `docs/explain/` | Before and after for each index of step 4, with the data volume stated |
| `docs/plan.md` | Marked with what was done and what was cut |
| Presentation | Slides in the repository, in a text format that renders without a tool the reviewer lacks |

## Acceptance criteria

- [ ] A person with Docker and nothing else gets the app running from the README alone.
- [ ] Every name in `CONTEXT.md` and every table in the ER diagram exists in the code under that
      name.
- [ ] The presentation fits in fifteen minutes when rehearsed aloud.
- [ ] For each of the role's emphases in `docs/plan.md` §2 there is one slide or one file to show.

## Tests

- None. The README's commands are run once, literally, on a clean clone.

## Out of scope

- New features, refactoring, anything that changes behaviour.

## Proposed outline

A proposal for the grooming session, in the order of the talk. The slides are written after the
deployment: the public URL and the screenshots come from it.

1. **Live demo** on the deployed address: the Dashboard, the Order List with its tabs, filters,
   search and pages, an order, and an operation recorded on it — after which the Orders badge and
   the dashboard tiles change.
2. **Domain model**: the ER diagram; why an alert follows a change of the quantity delta and not
   its presence; why the operations of a shipment are an append-only ledger.
3. **Architecture**: the layers, the command pipeline, one error format, why FastEndpoints and
   why the application layer does not depend on it.
4. **Database**: the indexes with `EXPLAIN ANALYZE` before and after; the transaction and the
   optimistic concurrency of the write flow.
5. **Tests and CI/CD**: the integration tests against a real PostgreSQL; the image that passed
   the checks is the image that is deployed.
6. **Beyond the brief**: the CSV export, the table layout of the list, sample data that keeps its
   dates fresh, the API reference behind the Help button.
7. **Left out on purpose, and why** — one slide:
   - the controls outside the three screens (New Order, Settings, Edit, BOL PDF, Share QR and the
     like) are marked in the pages as not part of the assignment;
   - photos and comments are stored as counts; file storage is the next step;
   - there is no sign-in: the current user is one seeded user;
   - resolving an alert by hand is a domain operation that no endpoint exposes;
   - one instance, no rate limiter.
8. **The AI workflow**: a step spec, the implementation, an independent review, the author's own
   check — who decides and what is delegated.
9. **The mockup's inconsistencies** and how the sample data was normalised.

The answers to the questions of *Open questions* 3 are prepared apart from the slides.

## Open questions

1. **Slide format.** Answered by the owner: a static HTML deck kept in the repository and
   published with GitHub Pages — dark background, large type, monospaced section labels, an
   accent gradient, a section bar at the bottom. The look is the deck's own; no one else's
   logo or text. GitHub Pages on the free plan needs a public repository, which is why the
   assignment's own materials (`assignment/`) leave the repository first: the mockup analysis
   moves to `docs/`, and if the repository becomes public its history is rewritten without
   that folder. The slides are in Ukrainian — the one exception, with `assignment/task.md`, to
   the English-only rule. Their content and order are agreed with the owner before any slide
   is written; the outline above is a starting point, not the decision.
2. **Which three or four files to walk through.** Candidates: `Order.cs`, the list query, the write
   handler, the CI workflow.
3. **Questions to prepare for.** The reviews of step 2 predicted these:
   - Why does every order show the client's own company as the customer?
   - How is a mistyped Unloading corrected in an append-only ledger?
   - What happens when the truck never shows?
   - Why one lock per order when each shipment has its own dock?
   - Why integers for enums when the SQL is written by hand?
   - Why migrate at startup?
4. **What to say about the AI workflow** and what to show: `CLAUDE.md`, the hooks, a step spec next
   to its review findings?
5. **Language** of the presentation and of the slides.
