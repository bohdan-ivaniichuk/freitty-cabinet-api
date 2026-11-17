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

## Open questions

1. **Slide format.** Marp or Slidev from Markdown, or a plain deck outside the repository?
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
