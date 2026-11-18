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
- **State lives in the URL, and every change of it is a plain navigation.** Tabs and paging are
  links; a filter or a search sets `location.search`. A link reproduces the view, Reload keeps it
  and Back works, with no history code to get wrong.
- **The server sends facts, the page words them.** Codes become labels in one module
  (`format.js`). One label per status on every page: `Draft` → "Draft", `Ready` → "New",
  `InProgress` → "In progress", `Completed` → "Completed". An order with an open Alert shows the
  "Alert" badge in place of its status, as the mockup does.
- **Text is written with `textContent`, never `innerHTML`**: order data is user input.
- **English only.** `lang="en"`. The Ukrainian design notes — the Pipeline view note, the Supply
  picker note, the Ukrainian `title` attributes — are removed: they describe features outside the
  assignment. The transfer banner is reworded in English.
- **What happens to each element of the mockup** — the default rule, with the exceptions listed
  under *Contract*:
  - text or a number that is data is filled from an endpoint;
  - a control without a function stays where it is, disabled: `disabled` on a button,
    `aria-disabled="true"` on anything else, no pointer cursor, and
    `title="Not part of this assignment"`;
  - static text, glyphs and decoration stay;
  - text that would be data and has no source is removed, never invented.
- **One order card.** The mockup draws two variants; the dashboard's — six fields, date with time
  — is used on both pages. The list keeps its three-column grid. The card's
  `<template id="order-card">` is repeated, identical, in `index.html` and `orders.html`.
- **One shell.** The mockup's three shells differ slightly; the dashboard's is used on all three
  pages, its markup repeated, identical, in the three files — there is no build step to include it
  from one place and no `innerHTML` to inject it.
- **Hub-local time and CAD.** Instants are formatted with `en-GB` in the zone the response names;
  money with `Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' })` → `$1,540.00`.
  No single locale gives both. The date shapes, all in `format.js`:

  | Shape | How |
  |---|---|
  | `12 Apr, 09:00` | `{ timeZone, day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }` |
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
    `div.tab`, `.oc-num`, the period buttons, Prev / Next, the crumbs, the nav item and the tile —
    `text-decoration: none; color: inherit`; the search input — `flex: 1; min-width: 0; border: 0;
    outline: 0; background: transparent; font: inherit; color: inherit`;
    `:disabled, [aria-disabled="true"] { cursor: default; opacity: .55 }`; loading, empty and error
    states; the Draft badge; the active period button.
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
├── app.css
└── js/
    ├── api.js        fetch wrapper: JSON, ProblemDetails → an error with status, code, title, detail, errors
    ├── format.js     labels for codes, dates in a zone, money, quantities, chart geometry — no DOM
    ├── shell.js      sidebar and top bar: GET /api/v1/me, the search box
    ├── card.js       fills one order card — shared by the dashboard and the list
    ├── dashboard.js
    ├── orders.js
    └── order.js
```

`Program.cs`: `UseDefaultFiles()` then `UseStaticFiles(…)`, after the exception handler and before
the request logging — so assets are not logged one line each — and before FastEndpoints. Static
files are sent with `Cache-Control: no-cache` (`StaticFileOptions.OnPrepareResponse`), so a new
deploy is picked up on the next load. `/` serves `index.html`; a file that does not exist falls
through to the `404` `ProblemDetails`. Each page has its own `<title>` ("Freitty — Dashboard",
"… — Orders", "… — Order") and `<link rel="icon" href="data:,">` — without it the browser asks
for a favicon that does not exist. The selects and the search input have `autocomplete="off"`, so
the browser's form restore does not fight the URL.

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
  every page, as in the mockup; its badge shows `needAttention` of `GET /api/v1/me` and is hidden
  at zero. "Settings" and "+ New Order" are disabled.
- Top bar: the balance, the user's initials and name. "Top up →", the bell and the help button
  are disabled; the bell's red dot is removed.
- The search box stays the mockup's `.fc-search` element with a borderless `<input>` inside it,
  placeholder "Search orders…". On `orders.html` it holds `q` and applies on Enter; clearing it and
  pressing Enter removes the search. On the other pages Enter goes to `orders.html?q=…`. The API
  searches from three characters: a shorter entry is not sent, and the input reports "Type at
  least 3 characters" as its validation message.
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
  - The period buttons are links that set `period`; "CW" is `week`. The active one carries the
    mockup's red style.
  - Header figures: `summary.completedLast30Days` and `summary.spendLast30Days`, each followed by
    "last 30d".
  - Both charts use the mockup's `0 0 400 120` view box: the baseline is y = 115, labels sit at
    y = 118. SVG nodes are created with `createElementNS('http://www.w3.org/2000/svg', …)`.
  - Completed orders, for n buckets: slot = 380 / n; bar i at x = 10 + i·slot, width 0.7·slot,
    height 110·value/max, y = 115 − height, `rx` 3, opacity 0.3 + 0.7·value/max; no rect for a
    zero.
  - Spend: point i at x = 10 + i·360/(n − 1), y = 115 − 100·value/max; the line through the
    points, the area closed down to y = 115, a dot per point; the highest point gets the mockup's
    larger dot with the white stroke.
  - Axis labels on the last bucket and every third before it (n − 1, n − 4, …), centred on the bar
    or the point: `Day` and `Week` — the start date as `12 Apr`; `Month` — `Apr`; `Quarter` —
    `Q2 2026`. `text-anchor` is `middle`, and `start` on point 0 of the line chart, where a
    centred label would be clipped.
  - A series that is all zeros draws the grid lines and the text "No data".
  - Summary: "📈 +20% completed orders vs previous 30 days" (green from zero up, red below; with a
    `null` percentage: "📈 {n} completed orders in the last 30 days"); "💰 {spend} spent · avg
    {average}/order" (without the average when it is `null`); "⭐ Best day / week / month /
    quarter: {its axis label} (peak spend {amount})", hidden when `best` is `null`.

### `orders.html`

Calls `GET /api/v1/orders`, `GET /api/v1/orders/counts`, `GET /api/v1/hubs`, `GET /api/v1/me`.

| URL parameter | Control | Sent to the API as |
|---|---|---|
| `tab` = `all` (default), `crossdock`, `consolidation`, `alerts`, `drafts` | the tabs, which become links | nothing, `type=CrossDock`, `type=Consolidation`, `alerts=true`, `status=Draft` |
| `hub` = a hub id | the first `<select>`: "Hub: All", then one option per hub of `/hubs` | `hub` |
| `date` = `today`, `week`, `last30` | the second `<select>`: "Date: Any" (added, the default), "Today", "This week", "Last 30 days" | `date=Today`, `ThisWeek`, `Last30Days` |
| `status` = `ready`, `inprogress`, `completed` | the third `<select>`: "Status: Any", "New", "In progress", "Completed" (added) | `status=Ready`, `InProgress`, `Completed` |
| `q` | the search box | `q` |
| `page` | Prev / Next | `page` |

- The filters are the mockup's three selects. A change navigates with the new value and without
  `page`; so does a tab and a search. A tab link keeps `hub`, `date`, `status` and `q`.
- A value the page does not know is treated as absent; so is a `q` shorter than 3 or longer than
  50 characters after trimming — the API would answer `400`. The input has `maxlength="50"`.
- On the Drafts tab the Status select is disabled and `status` from the URL is not sent to the
  list. `/orders/counts` always receives the URL's `status`.
- The tab counters come from `/orders/counts` with the same `hub`, `date`, `status` and `q`.
- "Showing {from}–{to} of {total}"; Prev and Next are disabled at the ends. An empty result says
  "No orders match these filters"; a page past the end shows that too, with Prev enabled.
- The page title stays "All Orders".
- Disabled: "Export CSV", "+ New Order", the Table and Pipeline buttons; Cards stays the active
  one. The Pipeline note is removed.

### The card (`card.js`)

- `.oc-num` becomes a link to `order.html?n={number}`. A click anywhere else on the card goes
  there too, through one delegated listener that ignores clicks inside `details`, `a`, `button`
  and `[aria-disabled]` — wrapping the card in a link would be invalid around the `<details>`.
- A card with an open Alert has the mockup's red top border. A draft has its dashed, faded style
  and the grey "DRAFT" badge.
- Badges: `badge-simple` "Cross-Dock" or `badge-consol` "Consolidation"; then `badge-new` "New",
  `badge-prog` "In progress", `badge-done` "Completed", or `badge-alert` "Alert" when any Alert is
  open.
- Ref N: one shipment → inline. Several → the `<details>` list, its count "{n} refs", open when a
  shipment has an open Alert. Each row: the sub-order label, the Ref N, "{n} pallets" — the actual
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
- Footer, from `next.code`: `ContinueEditing` → "Continue editing →" (a link to the detail);
  `AwaitingTruck` → "Next: Waiting for truck"; `Unloading` → "Next: Unloading"; `Loading` →
  "Next: Loading"; `AwaitingCompletion` → "Next: Completion"; `UploadPhoto` → "⚠ Next: Upload
  photo" and `ReviewQuantityDelta` → "⚠ Next: Review quantity", both in the mockup's red;
  `AwaitingPayment` → "Next: Payment · {amount} · #{invoice}"; `Paid` → "Paid · {amount} ·
  #{invoice}"; `Closed` → "Closed".
- The five footer icons, and a draft's delete icon, stay as inert glyphs.

### `order.html`

Calls `GET /api/v1/orders/{n}` and `GET /api/v1/me`. Without `n` it goes to `orders.html`.

**Header**

- The strip: "F", "Cross-Dock management" or "Consolidation management", the menu glyph, inert.
  The type badge keeps the mockup's inline blue style for both types.
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
- "⚠ Actual ≠ Expected" when `totals.quantityDeltas` is above zero, and after it a
  `badge badge-alert` "⚠ Missing photo" while any shipment has an open Missing photo alert.
- Icon bar: "💬 {comments}" and "📷 {photos}" as plain sums, without denominators; all its
  buttons, "Share QR", "BOL PDF" and "Edit" are disabled.
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
| Deltas detected | "{n} (q-ty)", also at zero; "View all →" stays, disabled |

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
  the shipment's open Alerts (`resolvedAt` is `null`). "+ Operation" is disabled here and becomes
  the form of [step 8](08-write-flow.md).

**Once per order, after the blocks**

- **Supplies**: SKU, a category badge (the mockup's three badge styles, chosen by category so one
  category keeps one style), quantity, unit price, line total, the two inert glyphs; the subtotal.
  Empty: "No supplies". "+ Supply" is disabled. "→ Invoice line items" and the picker note are
  removed.
- **Invoices**, when there are any: a block styled like the Supplies one, headed "Invoices", one
  line each — "#{number} · {amount} · paid {date}" or "· unpaid".

A draft renders the same page with dashes and "No shipments yet" in place of the blocks. An
unknown number shows a not-found state with the `detail` of the `ProblemDetails` and a link to the
list.

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

- [ ] `/`, the three pages, `app.css` and every script answer `200`; `/health/ready`, `/scalar`
      and `/api/v1/me` answer as before.
- [ ] `node --check` passes for every module, when Node is available. `format.js` touches no DOM:
      a throwaway `node -e "import(…)"`, not committed, prints one date in each shape, one amount,
      and the bar and point coordinates for 14, 12, 6 and 4 buckets, and they are what the rules
      above give.
- [ ] No seeded value and no Cyrillic in `wwwroot`, comments included:
      `grep -rnE 'FR0[0-9]|DRAFT-0|REF-[0-9]|TR[KL]-[0-9]|User [0-9]|R-way|Markham|Toronto|TForce|Schneider|\$[0-9]|[А-Яа-яІіЇїЄєҐґ]' src/Freitty.Api/wwwroot`
      prints nothing.
- [ ] The shell block is byte-identical in the three pages and the card template in the two that
      have it (`diff`).
- [ ] No `innerHTML`, `insertAdjacentHTML` or `document.write` in the scripts.
- [ ] `assignment/` is unchanged.
- [ ] Build with 0 warnings, `dotnet format --verify-no-changes` clean, `dotnet test` green.

*Browser session* — checked in a real browser against the running stack after the implementation,
and recorded in the step's review:

- [ ] The three pages look like the mockup's three screens.
- [ ] Every tab, filter, the search and Prev / Next change the URL and the list; Reload keeps the
      view; Back returns to the previous one.
- [ ] The tab counters equal the totals of the list for the same filters.
- [ ] A card opens its detail page; FR001383, a Consolidation (FR001676), a completed order
      (FR001672) and DRAFT-003 all render; an unknown number shows the not-found state.
- [ ] Each period button redraws both charts.
- [ ] The console has no error or warning on any page, and no request fails.

## Tests

- `/`, `/index.html`, `/orders.html`, `/order.html`, `/app.css` and each script are served with
  `200`, the right content type and `Cache-Control: no-cache` — integration.
- `/health/ready` and `/api/v1/me` still answer after static files are added — integration.

## Out of scope

- The Table and Pipeline views, "+ New Order", "Export CSV", "Top up", settings, chat.
- The form behind "+ Operation" — step 8.
- Browser tests in CI; a framework; a bundler.
