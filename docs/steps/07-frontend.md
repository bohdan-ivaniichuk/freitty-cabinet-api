# 07 — Frontend wiring

## Goal

The three screens of the mockup become three pages served by the API and filled from it:
Dashboard, Order List and Order Detail. The markup, class names and styles are the mockup's; only
the hardcoded data goes, replaced by what the endpoints of steps 4–6 return.

The source is [`assignment/Freitty_For_Candidate.html`](../../assignment/Freitty_For_Candidate.html),
which is read and never edited: sections `#dashboard`, `#orderlist` and `#crossdock`, and the
`<style>` rules they need.

## Decisions

- **Static pages, vanilla JavaScript, no build step** —
  [ADR 0012](../adr/0012-static-pages-served-by-the-api.md). ES modules loaded with
  `<script type="module">`.
- **Same origin.** The API serves the pages from `wwwroot`: no CORS, one deployable.
- **Only `.fc-shell` is the application.** The fake browser frame (`.mockup`, `.browser-bar`), the
  section headers, the document header, table of contents, footer and review toolbar are dropped,
  with the CSS that only they use. Class names inside `.fc-shell` are untouched.
- **Hardcoded cards and rows become `<template>` elements** with the same markup; the script
  clones and fills them.
- **State lives in the URL; within a page a change of view is made in place.** The three pages
  are separate static pages, and moving between them is an ordinary link. On `orders.html` a tab,
  a filter, a search, a page and the switch between Cards and Table rewrite the URL through the
  History API (`pushState`) and load the counters and the list again; on `index.html` a period
  button does the same and redraws only the activity block. Tabs, Prev / Next, Cards / Table and
  the period buttons stay links with their `href`: a plain click is answered in place, a modified
  click still opens a new tab. `popstate` handles Back and Forward. A copied link opens the same
  view and Reload keeps it. A page restored from the back/forward cache loads its data again.
- **The server sends facts, the page words them.** The API codes the scripts compare against or send
  are named once, in `codes.js`; they become labels in one module (`format.js`). One label per
  status on every page: `Draft` → "Draft", `Ready` → "New", `InProgress` → "In progress",
  `Completed` → "Completed". An order with an open Alert shows the "Alert" badge in place of its
  status, as the mockup does.
- **Text is written with `textContent`, never `innerHTML`**: order data is user input.
- **English only.** `lang="en"`. The Ukrainian design notes — the Pipeline view note, the Supply
  picker note, the Ukrainian `title` attributes — are removed: they describe features outside the
  assignment. The transfer banner is reworded in English.
- **What happens to each element of the mockup** — the default rule, with the exceptions listed
  under *Contract*:
  - text or a number that is data is filled from an endpoint;
  - a control without a function stays where it is, marked out of scope: `data-out-of-scope`,
    `aria-disabled="true"` — on a button too, which is not `disabled`, so it still takes focus and
    clicks — `title="Not part of this assignment"`, faded, no pointer cursor. A click on it, or
    Enter or Space on one that can take focus, shows the toast "Not part of this assignment" at
    the bottom of the page for about three seconds (`shell.js`, `format.OUT_OF_SCOPE`); a second
    one replaces it and starts its time again. A control disabled for a reason of its own — Prev /
    Next at the ends of the list, the Status filter on Drafts, the Hub filter when the hubs fail,
    the dialog's buttons while a request runs, "+ Operation" on an order that is not active — is
    not marked and shows no toast;
  - static text, glyphs and decoration stay;
  - text that would be data and has no source is removed, never invented.
- **One order card.** The mockup draws two variants; the dashboard's — six fields, date with time
  — is used on both pages. The list keeps its three-column grid. The card's
  `<template id="order-card">` is repeated, identical, in `index.html` and `orders.html`. The
  list's Table view shows the same orders as rows of `<template id="order-row">` inside
  `<template id="order-table">`, both only in `orders.html`; a card and a row are filled by the
  same code in `card.js`, so they word the same facts the same way.
- **One shell.** The mockup's three shells differ slightly; the dashboard's is used on all three
  pages, its markup repeated, identical, in the three files — there is no build step to include it
  from one place and no `innerHTML` to inject it.
- **Hub-local time and CAD.** Instants are formatted with `en-GB` in the zone the response names;
  money with `Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' })` → `$1,540.00`.
  No single locale gives both. The date shapes, all in `format.js`:

  | Shape | How |
  |---|---|
  | `12 Apr, 09:00` | `{ timeZone, day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }`; the month is cut to three letters everywhere — `en-GB` writes "Sept" |
  | `17 Apr 2026` | `{ timeZone, day: 'numeric', month: 'short', year: 'numeric' }` — also the "paid" date of an invoice |
  | `17 Apr · 08:55` | the day-and-month part and the time part of two formatters, joined by " · " |
  | "today" | the date in the hub's zone equals today's date in the hub's zone, both as `en-CA` `{ timeZone, dateStyle: 'short' }` strings |
  | a bucket's `start` | a calendar date, not an instant: formatted with `timeZone: 'UTC'`, never a hub's or the browser's zone — west of UTC it would print the day before. `12 Apr`, `Apr`; `Q2 2026` is computed |
- **`app.css` is the mockup's CSS for the shell, then one marked block of additions.**
  - *Kept, from the first `<style>` block only:* the reset and `html, body`; `.fc-*`; `.btn*`;
    `.badge*`; `.kpi*`; `.cards-grid`, `.order-card`, `.oc-*`, `.ref-n-inline`,
    `details.ref-n-multi`; `.tabs` and `.tab`; `.field*` (for the form of step 8). Comments that
    name seeded users are removed. Everything else, and the three later `<style>` blocks, is
    dropped. The mockup loads no font, image or script.
  - *Inline styles stay inline.* Most of the tiles, the chart and the detail page are styled with
    `style` attributes in the markup; they are copied as they are.
  - *Additions:* `.fc-shell { min-height: 100vh }` (it is 640px inside the frame);
    `.fc-main { min-width: 0 }` (the frame's `overflow: hidden` is gone); links that replace
    `div.tab`, `.oc-num`, the period buttons, Prev / Next, Export CSV, the crumbs, the nav item,
    the Help button and the tile — `text-decoration: none; color: inherit`; the search input —
    `flex: 1; min-width: 0; border: 0; outline: 0; background: transparent; font: inherit;
    color: inherit`; `:disabled, [aria-disabled="true"] { cursor: default; opacity: .55 }`; the
    `.toast`, faded in and out with `.is-shown`; loading, empty and error states; the Draft badge;
    the active period button; `.view-btn`, the Cards / Table links in the look of the mockup's
    buttons, the shown one (`aria-current`) pressed; `.order-table`, `.order-row` and `.ref-cell`
    for the Table view, in the look of the detail page's tables.
  - *States the mockup writes inline on one sample become modifier classes* with the mockup's
    values: the alert card's top border, the draft card, the red "Next" line, the orange
    "actual", the amber Actual card, the Disposal row, the red "today" date.

## Contract

### Files

```
src/Freitty.Api/wwwroot/
├── index.html      Dashboard
├── orders.html     Order List
├── order.html      Order Detail — order.html?n=FR001383
├── 404.html        the page a browser gets for an address with nothing behind it: the brand,
│                   "Page not found", a link to the dashboard
├── app.css
├── favicon.svg     the red "F" tile of the detail page's strip
└── js/
    ├── api.js        fetch wrapper: JSON, ProblemDetails → an error with status, code, title,
    │                 detail, errors; apiUrl, the address of an API path with its query
    ├── codes.js      the API codes the scripts compare against or send
    ├── format.js     labels for codes, most of the pages' wording (OUT_OF_SCOPE among it), dates in
    │                 a zone, money, quantities, chart geometry — no DOM
    ├── shell.js      sidebar and top bar: GET /api/v1/me, the search box; the toast of an
    │                 out-of-scope control; the loading, empty and error states; cloneTemplate,
    │                 fieldsOf, the "by" chip, isPlainClick, markCurrent
    ├── card.js       fills an order card and a row of the list's table — renderCard, renderTable,
    │                 openOrdersOnClick; shared by the dashboard and the list
    ├── dashboard.js
    ├── orders.js     the list: tabs, filters, search, pages, Cards / Table, the Export CSV link
    └── order.js
```

`Pages/StaticPages.cs`: `UseStaticPages()`, called from `Program.cs`, is `UseDefaultFiles()` then
`UseStaticFiles(…)`, after the exception handler and before the request logging — so assets are not
logged one line each — and before FastEndpoints. Static files are sent with
`Cache-Control: no-cache` (`StaticFileOptions.OnPrepareResponse`), so a new deploy is picked up on
the next load. `/` serves `index.html`.

A visitor who types an address gets a page, not JSON. `/orders` and `/order` redirect (`302`) to
`/orders.html` and `/order.html`, their query strings kept. Any other address that is not there, outside
`/api`, `/health`, `/openapi` and `/scalar`, answers `404` with `404.html` when the request's `Accept`
weighs `text/html` highest, as a browser's does (`Pages/NotFoundPage.cs`, chosen in the status-code
pages of `Errors/ErrorResponses.cs`). Everything else — the API, `fetch`, `curl`, any client that does
not prefer HTML — keeps the `404` `ProblemDetails`. `/favicon.ico` is left a `404`: every page names
`favicon.svg`, so only a client that ignores it asks. Each page has its own `<title>` ("Freitty — Dashboard", "… — Orders", "… — Order",
which the detail page turns into "Freitty — Order {number}") and
`<link rel="icon" href="favicon.svg" type="image/svg+xml">`. The selects and the search input have
`autocomplete="off"`, so the browser's form restore does not fight the URL.

Every response carries `X-Content-Type-Options: nosniff` and `Referrer-Policy: no-referrer`; the
three pages and `404.html` also carry a `Content-Security-Policy` that allows only the origin's own
scripts, styles, images and requests, inline `style` attributes, and no framing
(`Pages/SecurityHeaders.cs`). `/scalar` loads assets of its own and keeps the browser's defaults.

### Codes and their labels

| Field | Values → text |
|---|---|
| `status` | `Draft` → "Draft", `Ready` → "New", `InProgress` → "In progress", `Completed` → "Completed" |
| `type`, `crossDockKind` | `CrossDock` → "Cross-Dock", `Consolidation`; `Storage`, `Pickup` |
| `unitType` | `Standard`, `ExtraLarge` — worded per place below |
| `transport.kind` | `Carrier` → the carrier's name, `OwnDriver` → "Driver: {driverName}", `SelfPickup` → "Self pickup" |
| `arrival.trailerType` | `DryVan53` → "Van · 53ft", `Reefer53` → "Reefer · 53ft", `Flatbed48` → "Flatbed · 48ft" |
| Alert types | `QuantityDelta` → "quantity delta", `MissingPhoto` → "missing photo" |
| `role` | `Admin`, `Dispatcher` |
| `period` in the URL | `day`, `week`, `month`, `quarter`, sent as `Day`, `Week`, `Month`, `Quarter` |

### The shell (every page)

- The logo is a link to `index.html`. "Orders" is a link to `orders.html` and the active item on
  every page, as in the mockup; its badge shows `needAttentionCount` of `GET /api/v1/me` and is
  hidden at zero. "Settings" and "+ New Order" are out of scope.
- Top bar: the balance, the user's initials and name. "Top up →" and the bell are out of scope;
  the bell's red dot is removed. The help button, relabelled "API", is a link to the API
  reference, `/scalar/`, opened in a new tab (`target="_blank" rel="noopener"`,
  `title="API reference"`).
- The toast: `<div class="toast" id="toast" role="status" aria-live="polite">`, the last element
  of the shell block, so every page has it.
- The search box stays the mockup's `.fc-search` element with a borderless `<input>` inside it,
  placeholder "Search orders…". On `orders.html` it holds `search` and applies on Enter, in place;
  clearing it and pressing Enter removes the search. On the other pages Enter goes to
  `orders.html?search=…`. The API searches from three characters: a shorter entry is not sent, and
  the input reports "Type at least 3 characters" as its validation message. A search containing a
  control character is treated as absent.
- Breadcrumbs: "Home › Dashboard", "Home › Orders", "Orders › {number}" — "Home" and "Orders" are
  links.

### `index.html`

Calls `GET /api/v1/dashboard?period=…` and `GET /api/v1/me`. URL: `period` = `day`, `week`
(default), `month`, `quarter`. An empty `period=` is a `400` for the API: when the URL has none or
an unknown one, the page sends `Week`.

- "Welcome, {name} 👋".
- **Active Orders**: the count; "▲ n this week" in the mockup's green, "▼ n this week" in
  `#DC2626`, "no change this week" in `#6B7280`.
- **Completed (30 d)**: the count; "▲ n vs previous 30 d", "▼ n vs previous 30 d" or
  "⟶ same as previous 30 d", with the same colours.
- **Need Attention**: the total; "{n} · awaiting your action"; "{n} · alert" or "alerts" followed
  by the first of `alertOrders` in brackets — "(photo missing · FR001674)", "(quantity delta ·
  FR001383)", from its first Alert type. A chip whose number is zero is hidden. The whole tile is
  one link to `orders.html?tab=alerts`; "Open list →" stays a `<div>` inside it — a link inside a
  link is invalid.
- **Active Orders**: the cards of the response in the mockup's grid; "View all →" is a link to
  `orders.html`. Empty: "No active orders".
- **Your activity**:
  - The period buttons are links that set `period`; "CW" is `week`. A plain click rewrites the URL
    and redraws only the activity block. The active one carries the mockup's red style.
  - Header figures: `summary.completedLast30Days` and `summary.spendLast30Days`, each followed by
    "last 30d".
  - Both charts use the mockup's `0 0 400 120` view box. The baseline is y = 104 and the labels
    sit at y = 117: the mockup draws its baseline at 115, under the labels, which real data — a
    zero in the current bucket — turns into a dot drawn through its own label. SVG nodes are
    created with `createElementNS('http://www.w3.org/2000/svg', …)`.
  - Completed orders, for n buckets: slot = 380 / n; bar i at x = 10 + i·slot, width 0.7·slot,
    height 99·value/max, y = 104 − height, `rx` 3, opacity 0.3 + 0.7·value/max; no rect for a
    zero.
  - Spend: point i at x = 10 + i·360/(n − 1), y = 104 − 92·value/max; the line through the
    points, the area closed down to y = 104, a dot per point; the highest point gets the mockup's
    larger dot with the white stroke.
  - Axis labels on the last bucket and every third before it (n − 1, n − 4, …), centred on the bar
    or the point: `Day` and `Week` — the start date as `12 Apr`; `Month` — `Apr`; `Quarter` —
    `Q2 2026`. `text-anchor` is `middle`, and `start` on point 0 of the line chart, where a
    centred label would be clipped.
  - A series that is all zeros draws the grid lines and the text "No data".
  - Summary: "📈 +20% completed orders vs previous 30 days" (green from zero up, red below; with a
    `null` percentage: "📈 {n} completed orders in the last 30 days"); "💰 {spend} spent · avg
    {average}/order" (without the average when it is `null`); "⭐ Best day / week / month /
    quarter: {its axis label} (peak spend {amount})", hidden when `bestBucket` is `null`.

### `orders.html`

Calls `GET /api/v1/orders`, `GET /api/v1/orders/counts`, `GET /api/v1/hubs`, `GET /api/v1/me`.

The page's URL keeps its own names; the page maps them to the API's.

| URL parameter | Control | Sent to the API as |
|---|---|---|
| `tab` = `all` (default), `crossdock`, `consolidation`, `alerts`, `drafts` | the tabs, which become links | nothing, `type=CrossDock`, `type=Consolidation`, `hasOpenAlerts=true`, `status=Draft` |
| `hub` = a hub id | the first `<select>`: "Hub: All", then one option per hub of `/hubs` | `hubId` |
| `date` = `today`, `week`, `last30` | the second `<select>`: "Date: Any" (added, the default), "Today", "This week", "Last 30 days" | `datePreset=Today`, `ThisWeek`, `Last30Days` |
| `status` = `ready`, `inprogress`, `completed` | the third `<select>`: "Status: Any", "New", "In progress", "Completed" (added) | `status=Ready`, `InProgress`, `Completed` |
| `search` | the search box | `search` |
| `page` | Prev / Next | `page` |
| `view` = `table`; absent = cards | the Cards and Table links | nothing |

- The filters are the mockup's three selects. A change shows the view with the new value and
  without `page`; so does a tab and a search. A tab link keeps `view`, `hub`, `date`, `status` and
  `search`; a filter, a search and Prev / Next keep `view` too. While the new list loads, the old
  cards or table stay, dimmed (`aria-busy` on `#orders`).
- A value the page does not know is treated as absent, a `hub` that is not one of the loaded hubs
  included; so is a `search` shorter than 3 or longer than 50 characters after trimming, or with a
  control character — the API would answer `400`. A `page` outside 1–10 000 is the first page. The
  input has `maxlength="50"`.
- When `/hubs` fails, the Hub select reads "Hub: unavailable" and is disabled, and the list loads
  without a hub.
- On the Drafts tab the Status select is disabled and `status` from the URL is not sent to the
  list. `/orders/counts` always receives the URL's `status`.
- The tab counters come from `/orders/counts` with the same `hubId`, `datePreset`, `status` and
  `search`.
- "Showing {from}–{to} of {total}", and "Showing 0 of {total}" for an empty page; Prev and Next
  are disabled at the ends (no `href`, `aria-disabled="true"`). An empty result says "No orders
  match these filters"; a page past the end shows that too, with Prev enabled.
- The page title stays "All Orders".
- **Cards / Table.** The mockup's "⊞ Cards" and "☰ Table" buttons become links
  (`a.view-btn[data-layout]`) that set `view` and keep everything else, the page included; a
  plain click is answered in place, like a tab. The one shown carries `aria-current="page"`. Both
  show the same page of the same request: `view` is never sent to the API. The Table view is one
  table that spans the grid — see *The table row* below.
- **Export CSV** is a link, `<a id="export-csv" download>`, to `GET /api/v1/orders/export` with the
  filters of the list shown — the tab's included, the Drafts tab sending `status=Draft` in place of
  the URL's status — and without `page`. Its `href` is built with `apiUrl` and rebuilt on every
  render, so the file is always the list on screen, all its pages; the contract of the file is in
  [step 4](04-order-list.md).
- Out of scope: "+ New Order" and the "⎔ Pipeline" button. The Pipeline note is removed.

### The card (`card.js`)

- `.oc-num` becomes a link to `order.html?n={number}`. A click anywhere else on the card goes
  there too, through one delegated listener on the grid (`openOrdersOnClick`, on `#orders` and on
  the dashboard's `#active-orders`) that ignores clicks inside `details`, `a`, `button` and
  `[aria-disabled]` — wrapping the card in a link would be invalid around the `<details>`.
- A card with an open Alert has the mockup's red top border. A draft has its dashed, faded style
  and the grey "DRAFT" badge.
- Badges: `badge-simple` "Cross-Dock" or `badge-consol` "Consolidation"; then `badge-new` "New",
  `badge-prog` "In progress", `badge-done` "Completed", or `badge-alert` "Alert" when any Alert is
  open.
- Ref N: one shipment → inline. Several → the `<details>` list, its count "{n} refs". On the
  dashboard it is open when a shipment has an open Alert, as the mockup draws it; on the list it
  starts closed, as the mockup's list does — an open list is wider than a third of the page. The
  list's grid is `repeat(3, minmax(0, 1fr))`, and inside it the Ref N list may shrink and its
  description wrap, so a list the user opens does not push the columns apart; a Ref N itself never
  wraps. Each row: the sub-order label, the Ref N, "{n} pallets" — the actual
  quantity once anything was unloaded, the declared one before — and, for an open Alert,
  " · ⚠ missing photo" or " · ⚠ quantity delta" (both, comma-separated, when both are open).
- Type line: "Cross-Dock · Storage", "Cross-Dock · Pickup", "Consolidation · {n} sub-orders"
  ("1 sub-order"), and "{type label} · incomplete" for a draft without shipments, which has no
  Ref N element. A draft shows only the "DRAFT" badge, without the type badge, as in the mockup.
- "by" chip: initials in `.ava`, the name, the role tag — `.role` "Admin" or `.role.disp`
  "Dispatcher". A draft's card has no chip in the mockup; ours shows it like any other. The avatar
  colour class is `u1`–`u4`: `u{((sum of the initials' character codes + 2) mod 4) + 1}`, so one
  person always has one colour — the same rule on the detail page's chip.
- Fields: **Hub** — the name, or "Mark→Tor" from the short names with a transfer hub. **Date** —
  `12 Apr, 09:00` in the hub's zone, "—" when not scheduled. **Q-ty** — one Unit Type:
  "23 × Standard" or "10 × XL"; several: "15 × Std + 3 × XL"; with `hasQuantityDelta`:
  "20 decl · 18 actual", the second part in the mockup's orange; "—" without cargo.
  **Carrier** — the carrier's name, "Driver: {name}", "Self pickup" or "—". **Destination** —
  "Calgary, AB" or "—". **Trailers** — "{n} consolidated", only on a Consolidation with outbound
  trailers; without it Destination spans two columns as on the mockup's Cross-Dock cards.
- Footer, from `nextStep.code`: `ContinueEditing` → "Continue editing →" (a link to the detail);
  `AwaitingTruck` → "Next: Waiting for truck"; `Unloading` → "Next: Unloading"; `Loading` →
  "Next: Loading"; `AwaitingCompletion` → "Next: Completion"; `UploadPhoto` → "⚠ Next: Upload
  photo" and `ReviewQuantityDelta` → "⚠ Next: Review quantity", both in the mockup's red;
  `AwaitingPayment` → "Next: Payment · {amount} · #{invoice}"; `Paid` → "Paid · {amount} ·
  #{invoice}"; `Closed` → "Closed".
- The five footer icons, and a draft's delete icon, stay as inert glyphs.

### The table row (`card.js`)

The mockup draws no table; the Table view is ours. `renderTable` clones
`<template id="order-table">` — a `div.order-table` around a `<table>` with fixed column widths —
and appends one `<template id="order-row">` per order of the page, in the list's order. A row and a
card are filled by one function, `fillOrder`, so every text and badge below is the card's.

| Column | Content |
|---|---|
| Order # | `.oc-num`, the link to `order.html?n={number}`, and the type line under it |
| Status | the type badge and the status badge, as on the card — a draft shows only the Draft badge |
| Ref N | the Ref N of one shipment, "{n} refs" for several, "—" for none; one line, cut with an ellipsis, the whole text in its `title` |
| Hub, Date, Q-ty, Carrier, Destination | the card's fields, the orange "actual" included |
| Next | the card's footer line: "Continue editing →" as a link, the red "⚠ Next: …", the invoice number |

- A click anywhere on the row opens the order, through the same `openOrdersOnClick` listener as
  the cards (`.order-row`, `data-href`) — a link cannot wrap a table row. Clicks on a link inside
  it keep their own meaning. The row is highlighted on hover.
- A draft's row is faded (`.order-row.is-draft`). The row has no red top border: an open Alert
  shows as the "Alert" badge.
- The row leaves out what the card has beyond these columns: the Ref N list, the "by" chip,
  Trailers and the footer icons.

### `order.html`

Calls `GET /api/v1/orders/{n}` and `GET /api/v1/me`. Without `n` it goes to `orders.html`. An
`n` of `.` or `..`, which the URL parser folds into another path, and a number that reaches
another endpoint, such as `counts`, show the not-found state. The order number is the page's
`h1`; the sections and the dialog title are `h2`.

**Header**

- The strip: "F", "Cross-Dock management" or "Consolidation management", the menu button "☰",
  out of scope. The type badge keeps the mockup's inline blue style for both types.
- "←" is a link to `orders.html`. The number. The green "$" is shown when the order has an
  invoice, the truck glyph when transport is arranged. The type badge. "● On Stock" while any
  `totals.onStock` is above zero. The stage badge:

  | `stage` | Label | Class |
  |---|---|---|
  | `Draft` | Draft | the Draft badge |
  | `AwaitingTruck` | Waiting for truck | `badge-new` |
  | `Unloading` | Unloading in progress | `badge-prog` |
  | `Loading` | Loading in progress | `badge-prog` |
  | `AwaitingCompletion` | Loaded | `badge-prog` |
  | `Completed` | Completed | `badge-done` |

- Ref N inline for one shipment; "{n} sub-orders" for several.
- The "by" chip with the derived initials and the tag "ADMIN" or "DISP".
- "⚠ Actual ≠ Expected" when `totals.quantityDeltaCount` is above zero, and after it a
  `badge badge-alert` "⚠ Missing photo" while any shipment has an open Missing photo alert.
- Icon bar: "💬 {comments}" and "📷 {photos}" as plain sums, without denominators; all its
  buttons, "Share QR", "BOL PDF" and "Edit" are out of scope.
- The transfer banner only when the order has a transfer hub: "**Read-only:** transfers between
  hubs are arranged by the call center."

**Meta grid**

| Left column | |
|---|---|
| Customer | the company's name |
| Hub | "Markham (ON)"; with a transfer hub "Markham (ON) → Toronto" |
| Services | one badge each, alternating the mockup's two badge styles; "—" without any |
| Date | `17 Apr 2026`; " · today" and the red style only when that is today in the hub's zone; "—" when not scheduled |
| Declared q-ty | per Unit Type of `totals.declared`, separated by " + ": "10 Std · 48×40", "2 XL"; "—" when empty |
| Actual q-ty | per Unit Type of `totals.actual`, the same way — "0 Std · 48×40" before any Unloading, "—" when empty — with the badge "Δ +2" / "Δ −2", the sum of that Unit Type's non-null shipment deltas, when it is not zero |
| Trailer type | Cross-Dock only: "Van · 53ft", "Reefer · 53ft", "Flatbed · 48ft"; "—" before arrival |

| Right column | |
|---|---|
| Carrier | the carrier's name, "Driver: {name}", "Self pickup" or "—" |
| Phone | the carrier's phone or "—" |
| Destination | added: "Kingston, ON" or "—" |
| Truck / trailer | Cross-Dock only: "TRK-4521 / TRL-8830" or "—" |
| Dock | Cross-Dock only: "Dock 12 · Bay B" or "—" |
| Assigned to | Cross-Dock only: "User 6 (floor lead)" or "—" |
| Status flow | "Draft → New → In progress → Completed", the current status highlighted |
| Deltas detected | "{n} (q-ty)", also at zero; "View all →" stays, out of scope |

**Shipment block** — once for a Cross-Dock, once per shipment in order of position for a
Consolidation. A Consolidation's block is a white box like the meta grid. It starts with a header
— the sub-order label in bold, the Ref N in the mockup's `.ref-n-inline`, and a badge:
`badge-done` "Received", `badge-prog` "Receiving" (arrived, not received) or `badge-new`
"Expected" (not arrived) — and under it the meta grid's inner two-column grid with that
shipment's Trailer type, Truck / trailer, Dock and Assigned to, which its meta grid leaves out.
Then, for both types:

- **Dock card.** The wall reads "{HUB NAME} HUB"; the highlighted dock "DOCK {dock}"; the two
  flanking docks stay without numbers. Below: "Dock 12 · Bay B" and "Assigned 17 Apr, 08:42". One
  status line, the first that applies: the order is completed → "Released", no trailer box; the
  shipment has not arrived → "Waiting for the truck", no trailer box; otherwise the trailer box
  with the arrival's trailer number and "● Trailer docked" with a suffix of the shipment's own —
  " · unloading" while it is not received, " · loading" once it is received and has anything On
  stock, none otherwise. Without a dock, "No dock assigned" replaces the schematic and the two
  lines under it; the status line stays.
- **Expected (BOL)** and **Actual (warehouse)**: one line per row of the shipment's `quantities` —
  the number and "Standard · 48×40" or "XL". The Actual card has the mockup's amber style, the
  badge "+2" / "−2" and "Delta from BOL · read-only" only for a delta that is neither `null` nor
  zero; otherwise it is plain like Expected and says "Receiving in progress" before the shipment is
  received and "Matches BOL" after.
- **Warehouse note**: the note in quotation marks, or "—". The photo thumbnails are removed.
- **Operations**: per row, the green "$" for a billable one; a Disposal with the mockup's badge
  and red row; the trailer or "—"; the quantity; "Standard (48×40)" or "XL"; "17 Apr · 08:55" in
  the hub's zone; "💬 {n} · 📷 {n}" followed by the two inert glyphs. Empty: "No operations yet".
  The table's header shows, in the mockup's red, "⚠ missing photo" and "⚠ quantity delta" for
  the shipment's open Alerts (`resolvedAt` is `null`). "+ Operation" opens the form of
  [step 8](08-write-flow.md) on a Ready or In-progress order; on any other it stays `disabled`,
  titled "The order is not active", and is not marked out of scope.

**Once per order, after the blocks**

- **Supplies**: SKU, a category badge (the mockup's three badge styles, chosen by category so one
  category keeps one style), quantity, unit price, line total, the two inert glyphs; the subtotal.
  Empty: "No supplies". "+ Supply" is out of scope. "→ Invoice line items" and the picker note are
  removed.
- **Invoices**, when there are any: a block styled like the Supplies one, headed "Invoices", one
  line each — "#{number} · {amount} · paid {date}" or "· unpaid".

A draft renders the same page with dashes and "No shipments yet" in place of the blocks. An
unknown number shows a not-found state with the `detail` of the `ProblemDetails` and a link to the
list.

### Controls out of scope

Every one carries `data-out-of-scope`, `aria-disabled="true"` and
`title="Not part of this assignment"` in the HTML, and answers with the toast.

| Page | Controls |
|---|---|
| every page (the shell) | "⚙️ Settings", the sidebar's "+ New Order", "Top up →", the bell |
| `index.html` | none beyond the shell's |
| `orders.html` | "+ New Order" beside "Export CSV", "⎔ Pipeline" |
| `order.html` | the strip's "☰" menu; the icon bar's six buttons (💬, 📷, $, 🖨, 📦, 🕘); "📷 Share QR", "📥 BOL PDF", "✏️ Edit"; "View all →" beside Deltas detected; "+ Supply" |

Every one takes focus and answers Enter and Space as well as a click: a `<button>` by itself, any
other element through `role="button"` and `tabindex="0"` — the bell, the "☰" menu, "Top up →",
"View all →", and "Settings". "Settings" carries them on a `<span>` inside its `<li>`, so the list
keeps its list items. The footer icons
of a card, the "⧉ · 🗑" after an operation's counters and the "✏️ · 🗑" of a supply row are text,
not controls: they stay inert, without the marker.

### Removed for lack of data

The countdown in "Next: Loading · 2h 14m"; "via External PDF" and "Order with photos" beside a
destination; the denominators of the photo and comment counters; the photo thumbnails; the numbers
of the neighbouring docks; "→ Invoice line items"; the bell's dot.

### States

Every block that fetches has a loading state ("Loading…"), an empty state and an error state that
shows the `title` and `detail` of the `ProblemDetails`. A failed request never leaves sample
values on screen — there are none in the HTML.

## Acceptance criteria

What the implementation session checks itself, on `docker compose up -d --build --wait`:

- [ ] `/`, the three pages, `app.css`, `favicon.svg` and every script answer `200`;
      `/health/ready`, `/scalar` and `/api/v1/me` answer as before.
- [ ] `/orders?status=Draft` redirects to `/orders.html?status=Draft`; `/nonexistent` in a browser
      shows "Page not found" with status `404`, and with `Accept: application/json` is the `404`
      `ProblemDetails`; an unknown `/api/v1/…` is the `ProblemDetails` for every client.
- [ ] A page carries `Content-Security-Policy`, `X-Content-Type-Options` and `Referrer-Policy`, an
      API response the last two; the pages work under the policy and `/scalar` still renders.
- [ ] `node --check` passes for every module, when Node is available. `format.js` touches no DOM:
      a throwaway `node -e "import(…)"`, not committed, prints one date in each shape, one amount,
      and the bar and point coordinates for 14, 12, 6 and 4 buckets, and they are what the rules
      above give.
- [ ] No seeded value and no Cyrillic in `wwwroot`, comments included:
      `grep -rnE 'FR0[0-9]|DRAFT-0|REF-[0-9]|TR[KL]-[0-9]|User [0-9]|R-way|Markham|Toronto|TForce|Schneider|\$[0-9]|[А-Яа-яІіЇїЄєҐґ]' src/Freitty.Api/wwwroot`
      prints nothing.
- [ ] The shell block, the toast included, is byte-identical in the three pages and the card
      template in the two that have it (`diff`).
- [ ] Every element with `title="Not part of this assignment"` carries `data-out-of-scope` and
      `aria-disabled="true"`, and none has `disabled`.
- [ ] No `innerHTML`, `insertAdjacentHTML` or `document.write` in the scripts.
- [ ] `assignment/` is unchanged.
- [ ] Build with 0 warnings, `dotnet format --verify-no-changes` clean, `dotnet test` green.

*Browser session* — checked in a real browser against the running stack after the implementation,
and recorded in the step's review:

- [ ] The three pages look like the mockup's three screens.
- [ ] Every tab, filter, the search, Prev / Next and Cards / Table change the URL and the list;
      Reload keeps the view; Back returns to the previous one.
- [ ] The Table view shows the same orders as the cards of the same page, and keeps `view=table`
      across tabs, filters, search and pages.
- [ ] The tab counters equal the totals of the list for the same filters.
- [ ] "Export CSV" downloads `orders.csv` with the orders of the view shown, all its pages.
- [ ] The help button opens the API reference in a new tab.
- [ ] A click on each out-of-scope control shows the toast once; a control disabled for a reason
      of its own shows none.
- [ ] A card and a table row open their detail page; FR001383, a Consolidation (FR001676), a
      completed order (FR001672) and DRAFT-003 all render; an unknown number shows the not-found
      state.
- [ ] Each period button redraws both charts.
- [ ] The console has no error or warning on any page, and no request fails.

## Tests

- `/`, `/index.html`, `/orders.html`, `/order.html`, `/app.css` and each script are served with
  `200`, the right content type and `Cache-Control: no-cache` — integration.
- `/health/ready` and `/api/v1/me` still answer after static files are added — integration.

## Out of scope

- The Pipeline view, "+ New Order", "Top up", settings, notifications, chat, and the other
  controls listed under *Controls out of scope*.
- Sorting the Table view by a column.
- The form behind "+ Operation" — step 8.
- Browser tests in CI; a framework; a bundler.
