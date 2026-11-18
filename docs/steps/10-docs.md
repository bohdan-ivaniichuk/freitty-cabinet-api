# 10 — Docs and presentation

## Goal

What the reviewers read before the live demo is complete and true to the code: a README that
guides them through the project, the decision records, the schema, the query plans, and a slide
deck they can read on their own.

## Decisions

- **Nothing is written here that could have been written with the code.** ADRs, the ER diagram,
  the glossary and the query plans are produced by the steps that made the decisions. This step
  checks them against the final code and fills what is left.
- **The README is a reviewer's guide, not a product page.** Its links open the deployed demo; a
  table says where to start reading the code; the operator's commands stay in the runbook
  ([`deployment.md`](../deployment.md)).
- **The deck is read without a narrator.** The reviewers get it before the live demo, so every
  slide carries a claim as its heading, the evidence under it — a real excerpt of the code, a
  diagram, a number — and a link to the file. The slides are in Ukrainian, the one exception to
  the English-only rule.
- **The deck is a static page in the repository** (`presentation/`: one HTML file, one stylesheet,
  one script, no framework and no build step), published with GitHub Pages by its own workflow.
  GitHub Pages on the free plan needs a public repository, so the workflow does nothing until
  Pages is enabled.
- **The deck follows the order of the work**: the scope taken and why, how the steps went and how
  AI tools were used and kept in check, the domain, the database, the implementation with what
  was taken from DDD, CQRS, Clean Architecture and REPR and what was not, the result.
- **The assignment's own materials are not kept in the repository** — the brief and the original
  mockup were removed by a commit; the analysis of the mockup stays in
  [`mockup-analysis.md`](../mockup-analysis.md).
- **Known limits are stated**, in the README and on the last slide, from the consequences the
  ADRs record.

## Contract

| Document | State after this step |
|---|---|
| `README.md` | What the project is, where to open it running, where to start reading, how it is built and released, known limits, how to run it locally |
| `presentation/` | The deck: five sections, each slide standing on its own; screenshots of the deployed app in `presentation/img/` |
| `.github/workflows/pages.yml` | Publishes `presentation/` to GitHub Pages on a push that changes it, once Pages is enabled |
| `docs/adr/` | Every decision a reviewer could question has a record; none describes something the code no longer does |
| `docs/er-diagram.md` | Matches the migrations, the indexes and the sequence included |
| `CONTEXT.md` | Matches the names in the code |
| `docs/explain/` | Before and after for each index, with the data volume stated |

## Acceptance criteria

- [ ] A person with Docker and nothing else gets the app running from the README alone.
- [ ] Every link of the README's first screens opens on the deployed demo or in the repository.
- [ ] Every number and every excerpt in the deck can be found in the repository as shown.
- [ ] The deck opens from a file and from GitHub Pages, moves with the keyboard, and prints one
      slide per page.

## Tests

- None. The README's links and the deck's links are opened once.

## Out of scope

- New features, refactoring, anything that changes behaviour.
