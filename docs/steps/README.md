# Step specs

One file per implementation step from [`docs/plan.md`](../plan.md) §10, named `NN-<step>.md`
(`01-skeleton.md`, `04-order-list.md`). A spec is written during grooming, before any code, and is
the only input the implementation session gets besides `CLAUDE.md` — if the implementer has to ask,
the spec has a gap.

Keep it short. Template:

```markdown
# NN — <Step name>

## Goal
What this step delivers, in two or three sentences.

## Decisions
- <decision> — <why>. Link an ADR when the decision is architectural.

## Contract
Endpoints, request/response shapes, schema changes — whatever this step exposes.

## Acceptance criteria
- [ ] <observable, checkable outcome>

## Tests
- <behaviour to cover> — unit | integration

## Out of scope
- <what is deliberately left for a later step>
```
