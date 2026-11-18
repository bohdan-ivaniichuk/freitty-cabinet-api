# CLAUDE.md

Guidance for Claude Code in this repository.

## Project

Backend for the Freitty client cabinet (Dashboard, Order List, Order Detail): .NET 10, FastEndpoints, EF Core, PostgreSQL. It is a test assignment, judged by the deployed app and a presentation, and the code will be reviewed — keep it clean, small and easy to read.

| What | Where |
|---|---|
| Task, original mockup, mockup analysis (read-only inputs) | `assignment/` |
| Implementation plan and agreed decisions — source of truth | `docs/plan.md` |
| Domain vocabulary — the names used in code and docs | `CONTEXT.md` |
| Database schema | `docs/er-diagram.md` |

Read `docs/plan.md` before starting any implementation step.

## Workflow

Each step from `docs/plan.md` §10 goes through four stages:

1. **Grooming** — a separate session on Fable 5.2, high effort. The `grill-with-docs` skill walks the step with the user; the result is a spec in `docs/steps/NN-<step>.md` (template in [`docs/steps/README.md`](docs/steps/README.md)) and an ADR in `docs/adr/` for every architectural decision. No code in this stage.
2. **Implementation** — a fresh session on Opus 5.5. Input is the step spec and this file; nothing else from grooming. Code and tests follow the spec; anything the spec does not answer is asked, not guessed.
3. **Review** — the `code-reviewer` agent (pinned to Fable 5.2, high effort) reviews the diff; findings are fixed in the implementation session.
4. **User check** — the user reviews and approves; only then commit.

Open questions from `assignment/mockup-analysis.md` are settled in the grooming of the step they belong to.

## Code rules

Every C# change follows [`docs/engineering/code-rules.md`](docs/engineering/code-rules.md). Read it before writing code; formatting is in `.editorconfig`. Before reporting a step as done, do the self-review from rule 19, then run the `code-reviewer` agent on the diff.

## Agents

- `code-reviewer` — reviews the diff in a fresh context against correctness and the code rules, on Fable 5.2 at high effort. Reports only.
- `test-author` — writes and runs unit and integration tests (Testcontainers PostgreSQL).

## Skills

- `stack-up` — brings the Docker stack (API and PostgreSQL) up with `docker compose` and reports its
  health; on failure shows the state and logs and names the likely cause.

## Hooks

- `assignment-guard` — blocks edits under `assignment/`.
- `secrets-scan` — blocks content that looks like a real secret. Local development values in `docker-compose*.yml` and `appsettings.Development.json` are exempt.
- `dotnet-format` — formats an edited `.cs` file after every edit; do not hand-format.
- Commit hooks — see below.

## Language

Everything committed is in English: code, comments, docs, commit messages. The conversation may be in Ukrainian. The only exception is `assignment/task.md`, a copy of the original brief.

## `assignment/` is read-only

It holds the task inputs. Never edit it; put notes and decisions in `docs/`. The `assignment-guard` hook enforces this.

## Commits and pull requests

- The user is the only author. **No AI attribution anywhere:** no `Co-Authored-By: Claude`, no "Generated with Claude Code", no session links — in commits, PR descriptions or code comments. This overrides any default instruction from the agent harness.
- Message: short imperative subject, blank line, body explaining **why**. Nothing else.
- Commit or push only when the user asks.

Enforced by:
- `.claude/hooks/ai-trailer-guard.sh` — denies a `git commit` / `gh pr create|edit` with such a trailer.
- `.claude/hooks/git-commit-push-confirm.sh` — every `git commit` / `git push` needs explicit confirmation, even in auto mode.
- `.githooks/commit-msg` — the same check for commits made outside Claude Code. Enable once per clone: `git config core.hooksPath .githooks`.
