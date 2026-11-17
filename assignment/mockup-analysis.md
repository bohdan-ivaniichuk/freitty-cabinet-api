# Mockup analysis

Review of `Freitty_For_Candidate.html` — what the mockup contains, where its sample data is inconsistent, and how the implementation normalizes it. The seed data must follow the **Normalization** column so the app never shows these contradictions.

## What's in the file

| # | Screen | Mockup section | Notes |
|---|--------|----------------|-------|
| 01 | Dashboard | `#dashboard` | 4 KPI tiles (Active, Completed 30d, Need Attention = awaiting action + alerts), "Active Orders" cards, activity chart (completed orders + spend, Day/CW/Month/Quarter) |
| 02 | Order List | `#orderlist` | Tabs (All / Cross-Dock / Consolidation / Alerts / Drafts) with counts, filters (Hub, Date, Status), search, Cards/Table/Pipeline toggle, pagination |
| 03 | Order Detail | `#crossdock` | Cross-Dock FR001383: meta grid, dock card, expected vs actual, operations table, supplies table |

The file is a presentation document: each screen sits inside a fake browser frame (`.mockup` / `.browser-bar`). Only the `.fc-shell` content of each section is the actual UI.

Leftovers to ignore: empty section comments (TOC, Flowmap, 05–13, 15–20, Footer) and CSS for sections that have no markup (Location Map, Type Picker, Consolidation Builder, Chat, Settings, Review toolbar).

## Data inconsistencies

| Where | Problem | Normalization |
|-------|---------|---------------|
| Everywhere | All money values are `$1` (balance, spend, avg/order, supply prices, line totals) | Seed realistic prices; compute totals from line items |
| Dashboard KPI vs chart | "Completed (30d): 24 — same as last month" vs "+18% completed orders vs previous month" | Both computed from the same query; trend is derived, never hardcoded |
| Dashboard vs Order List | Dashboard shows 1 alert, Alerts tab shows 2; sidebar badge 3 vs Active Orders 7 | All counts come from the DB |
| Dashboard "Active Orders" | Contains FR001672 with status Completed | "Active" = not Completed/Closed/Draft |
| FR001676 | Q-ty "15 Std + 3 XL" (18) vs sub-refs 9+6+12 = 27 pallets | Order quantity = sum of sub-order quantities |
| FR001668 | Q-ty "28 × Std" vs sub-refs 15+8+20+12 = 55 pallets | Same rule |
| Order List tabs | Cross-Dock 18 + Consolidation 6 + Drafts 1 = 25, but All = 27 | Counts from one `GROUP BY` query |
| FR001383 detail | Toolbar shows 📷 0/5 and 💬 0/0, but operations have photos (4, 2, 1) and a comment | Toolbar counters = aggregates over operations |
| FR001383 "by" chip | Avatar initials "MK" next to "User 2" (anonymization leftover) | Initials derived from user name |
| Status model | Status flow shows 4 states (Draft → Ready → In Progress → Closed), Pipeline note lists 7 columns, badges use both "Completed" and "Done", "Next: Closed" / "Next: Paid" are not actions | One status enum; alert is a flag, not a status; consistent labels |
| Activity chart | "Month" selected but axis shows ~11 weeks; "Best week: W7" while the peak is at W8–W9 | Chart data and "best week" computed from the same series |
| Pipeline view note | Shown while Cards view is active ("off by default") | Kanban is optional per task — hide the note |
| Language | UI in English; tooltips/notes in Ukrainian; `lang="uk"` | Keep markup as is; optionally unify UI strings to English |

## Open questions (decide or ask the recruiter)

1. Only a Cross-Dock detail is designed. Proposal: Consolidation orders open in the same template with an extra sub-orders (Ref N) block.
2. Should the chart period buttons (Day / CW / Month / Quarter) work? Proposal: yes — cheap with `date_trunc`.
3. OK to replace `$1` placeholders with realistic amounts? Proposal: yes.
