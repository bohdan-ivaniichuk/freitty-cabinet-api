# 07 — Frontend wiring

> **Draft, not groomed.** Written ahead of the grooming session so it does not start from zero.
> Everything under *Decisions* is a proposal; *Open questions* is what the session has to settle.
> Nothing here is input for implementation yet.

## Goal

The three screens of the mockup become three pages served by the API and filled from it. Markup,
styles and fields stay as they are in the mockup; only the hardcoded data goes.

## Decisions

- **Three static pages** in `src/Freitty.Api/wwwroot`: `index.html` (Dashboard), `orders.html`
  (Order List), `order.html?n=FR001383` (Detail). The fake browser frame is dropped; `.fc-shell`
  and every class name stay. Shared styles move to `app.css`.
- **Same origin.** The API serves the pages as static files: no CORS, one deployable.
- **No framework and no build step.** Hardcoded cards become `<template>` elements with the same
  markup; a small vanilla script fetches, clones and fills. The task says the markup is done; what
  is judged is the backend.
- **State lives in the URL.** Tabs, filters, search and paging change query parameters and refetch,
  so a link reproduces the view and Back works.
- **The server sends facts, the page words them.** Status and "Next" codes are mapped to the
  mockup's labels in one place in the script ("Ready" → "New"). Instants are formatted in the
  hub's zone with `Intl.DateTimeFormat`.
- **Text is written with `textContent`**, never `innerHTML`: order data is user input.
- `assignment/` is not touched; the pages are copies cut from it.

## Contract

| Page | Calls |
|---|---|
| `index.html` | `GET /api/v1/dashboard`, the shell data |
| `orders.html` | `GET /api/v1/orders`, `GET /api/v1/orders/counts`, the shell data |
| `order.html` | `GET /api/v1/orders/{number}`, the shell data |

- A card links to `order.html?n=<number>`.
- Loading, empty and error states exist for every block that fetches: an empty list says so, a
  `ProblemDetails` shows its title.
- Elements of the mockup that have no data behind them are listed in step 2 (*Out of scope*) and
  are removed or left static — never filled with invented values.

## Acceptance criteria

- [ ] `docker compose up` and `http://localhost:8080/` shows the dashboard with seeded data; the
      three pages look like the mockup's three screens.
- [ ] No order number, name or amount is written in the HTML or the script.
- [ ] Every tab, filter, the search and Prev / Next change the URL and the list; reloading the page
      keeps the view.
- [ ] A card opens its detail page; an unknown number shows a not-found state.
- [ ] The browser console is clean on all three pages.

## Tests

- The three pages and the stylesheet are served with `200` — integration
- The rest is checked by hand against the mockup, page by page, and recorded in the step's review.

## Out of scope

- Table and Pipeline views, "+ New Order", "Export CSV", "Top up", settings, chat, the global
  search box in the header.
- Browser tests in CI.

## Open questions

1. **Labels.** The mockup calls one status "Completed" on the dashboard and "Done" in the list, and
   another "New". One label per status everywhere, or keep each screen's wording?
2. **Language.** UI text is English; tooltips and notes are Ukrainian; the page says `lang="uk"`.
   Unify to English or leave?
3. **Mockup elements without data**: remove them, or leave them as static decoration? Per element:
   the countdown, the counters' denominators, "via External PDF", the Pipeline note, the Transfer
   banner.
4. **Buttons that do nothing** ("+ New Order", "Edit", "Share QR"): disabled, hidden, or left
   clickable with no effect?
5. **The Consolidation detail** has no design. How far does this step go — see step 5, question 7.
6. **"+ Operation"** on the detail page belongs to step 8. Build its form there, or here as a
   disabled button?
7. **The chart.** The mockup draws it with inline SVG. Redraw the same SVG from the series, or is a
   tiny chart library acceptable?
8. **Any automated check of the pages** beyond "served with 200" — a headless smoke test — or is
   the manual pass enough for an assignment judged by a demo?
