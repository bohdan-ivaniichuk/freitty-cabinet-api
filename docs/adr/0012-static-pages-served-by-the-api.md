# 0012 — Static pages with vanilla JavaScript, served by the API

- Status: accepted
- Date: 2026-11-18

## Context

The assignment hands over finished markup — one HTML file with three screens — and asks for the
backend that feeds it. The result is judged by the deployed application, on a free hosting tier,
and the code is reviewed for the backend it contains.

## Decision

The three screens are cut into three static HTML pages that keep the mockup's markup and class
names. A few small ES modules fetch the API, clone `<template>` elements and fill them with
`textContent`. There is no framework and no build step.

The API serves the pages from `wwwroot`, on the same origin as `/api/v1`.

## Alternatives

- **A single-page application (React, Vue)** — components, routing and state management for free.
  Rejected: it rewrites markup the task calls done, adds a toolchain and a build stage to the image
  and the pipeline, and moves the reviewer's attention to code the role is not about.
- **Server-rendered pages (Razor, Blazor)** — no JavaScript to write. Rejected: the API would stop
  being the contract. The screens must be fed through endpoints, and those endpoints are what the
  assignment asks to see.
- **A separate static host for the pages** — the usual production shape, with a CDN in front.
  Rejected: two deployables, CORS and a second pipeline for three files.

## Consequences

- One image, one URL, no CORS, nothing to build but the API.
- The pages keep the mockup's markup and classes; what differs is listed in the step's spec —
  sample data removed, controls without a function disabled, design notes dropped.
- The API contract is exercised by a real client: every value on screen comes from an endpoint.
- Moving between the three pages is an ordinary link and a full page load. Within a page, a change
  of view — a tab, a filter, a search or a page of the list, a period of the chart — is made in
  place: the URL is rewritten through the History API and only the affected blocks are loaded
  again. The cost is a little history code per page — `pushState`, a `popstate` handler, plain
  clicks told from modified ones — and a page restored from the back/forward cache must load its
  data again.
- No components: the order card is shared by two pages as a module and a `<template>` copied into
  both. Accepted at three pages; a fourth page with cards is the point to reconsider.
- No client-side tests. The pages are checked in a browser against the running stack; the
  behaviour worth automating lives behind the API and is tested there.
- State is in the URL, so a link reproduces a view, and Reload and Back keep working, without a
  client-side router.
