// orders.html: the list of GET /api/v1/orders with its tabs, filters, search and pages. The view
// lives in the URL; every change of it is a navigation to the new URL.

import { getJson } from './api.js';
import { openCardsOnClick, renderCard } from './card.js';
import { DASH } from './format.js';
import { describeError, errorBox, initShell, loadMe, searchText, stateBox } from './shell.js';

// Each tab is a filter of the list, and a field of GET /api/v1/orders/counts.
const TABS = {
  all: { parameters: {}, count: 'all' },
  crossdock: { parameters: { type: 'CrossDock' }, count: 'crossDock' },
  consolidation: { parameters: { type: 'Consolidation' }, count: 'consolidation' },
  alerts: { parameters: { alerts: 'true' }, count: 'alerts' },
  drafts: { parameters: { status: 'Draft' }, count: 'drafts' },
};
const DEFAULT_TAB = 'all';
// The filters' URL values and the API values they stand for.
const DATE_FILTERS = { today: 'Today', week: 'ThisWeek', last30: 'Last30Days' };
const STATUS_FILTERS = { ready: 'Ready', inprogress: 'InProgress', completed: 'Completed' };
const FILTERS = ['hub', 'date', 'status'];
const FIRST_PAGE = 1;
const MAX_PAGE = 10000;
const NO_MATCH = 'No orders match these filters';

let view = readView(new URLSearchParams(location.search));

initShell({ query: view.q ?? '', searchUrl: q => viewUrl({ ...view, q, page: FIRST_PAGE }) });
loadMe();
openCardsOnClick(document.getElementById('order-cards'));
bindFilters();
start();

// A value the page does not know is treated as absent: sent on, the API would answer 400. The hub is
// checked once the hubs are loaded.
function readView(url) {
  const known = (values, name) => (Object.hasOwn(values, url.get(name)) ? url.get(name) : null);
  const page = Number(url.get('page'));
  return {
    tab: known(TABS, 'tab') ?? DEFAULT_TAB,
    hub: url.get('hub'),
    date: known(DATE_FILTERS, 'date'),
    status: known(STATUS_FILTERS, 'status'),
    q: searchText(url.get('q')),
    page: Number.isInteger(page) && page >= FIRST_PAGE && page <= MAX_PAGE ? page : FIRST_PAGE,
  };
}

function viewUrl(next) {
  const url = new URLSearchParams();
  if (next.tab !== DEFAULT_TAB) {
    url.set('tab', next.tab);
  }

  for (const name of [...FILTERS, 'q']) {
    if (next[name]) {
      url.set(name, next[name]);
    }
  }

  if (next.page > FIRST_PAGE) {
    url.set('page', next.page);
  }

  const query = url.toString();
  return query ? `orders.html?${query}` : 'orders.html';
}

async function start() {
  const hubs = await loadHubs();
  view = { ...view, hub: hubs?.some(hub => hub.id === view.hub) ? view.hub : null };
  renderTabs();
  renderFilters();
  loadCounts();
  loadList();
}

function bindFilters() {
  for (const name of FILTERS) {
    const select = document.getElementById(`filter-${name}`);
    select.addEventListener('change', () =>
      location.assign(viewUrl({ ...view, [name]: select.value || null, page: FIRST_PAGE })));
  }
}

// Without the hubs the Hub filter cannot be shown, and the list is loaded without it.
async function loadHubs() {
  const select = document.getElementById('filter-hub');
  try {
    const hubs = await getJson('/api/v1/hubs');
    select.append(...hubs.map(hub => new Option(hub.name, hub.id)));
    return hubs;
  } catch (error) {
    select.options[0].textContent = 'Hub: unavailable';
    select.title = describeError(error).detail;
    select.disabled = true;
    return null;
  }
}

function renderTabs() {
  for (const tab of document.querySelectorAll('[data-tab]')) {
    const isActive = tab.dataset.tab === view.tab;
    tab.href = viewUrl({ ...view, tab: tab.dataset.tab, page: FIRST_PAGE });
    tab.classList.toggle('active', isActive);
    if (isActive) {
      tab.setAttribute('aria-current', 'page');
    }
  }
}

// Drafts is a status of its own: on that tab the Status filter does not apply.
function renderFilters() {
  for (const name of FILTERS) {
    document.getElementById(`filter-${name}`).value = view[name] ?? '';
  }

  document.getElementById('filter-status').disabled = view.tab === 'drafts';
}

function filterParameters() {
  return { hub: view.hub, date: DATE_FILTERS[view.date], status: STATUS_FILTERS[view.status], q: view.q };
}

async function loadCounts() {
  const countOf = tab => document.querySelector(`[data-tab="${tab}"] .count`);
  try {
    const counts = await getJson('/api/v1/orders/counts', filterParameters());
    for (const [tab, { count }] of Object.entries(TABS)) {
      countOf(tab).textContent = counts[count];
    }
  } catch (error) {
    for (const tab of Object.keys(TABS)) {
      countOf(tab).textContent = DASH;
    }

    const failure = document.getElementById('counts-error');
    failure.replaceChildren(errorBox(error));
    failure.hidden = false;
  }
}

async function loadList() {
  const grid = document.getElementById('order-cards');
  const parameters = {
    ...filterParameters(),
    ...TABS[view.tab].parameters,
    page: view.page > FIRST_PAGE ? view.page : null,
  };
  try {
    const result = await getJson('/api/v1/orders', parameters);
    grid.replaceChildren(...(result.items.length === 0 ? [stateBox(NO_MATCH)] : result.items.map(order => renderCard(order))));
    renderPager(result);
  } catch (error) {
    grid.replaceChildren(errorBox(error));
  }
}

// A page past the end shows nothing, with Prev leading back to the last page.
function renderPager({ items, page, pageSize, total }) {
  const from = (page - 1) * pageSize + 1;
  document.getElementById('showing').textContent = items.length === 0
    ? `Showing 0 of ${total}`
    : `Showing ${from}–${from + items.length - 1} of ${total}`;

  const lastPage = Math.max(FIRST_PAGE, Math.ceil(total / pageSize));
  setPageLink(document.getElementById('page-prev'), page > FIRST_PAGE ? Math.min(page - 1, lastPage) : null);
  setPageLink(document.getElementById('page-next'), page < lastPage ? page + 1 : null);
}

function setPageLink(link, page) {
  if (page === null) {
    link.removeAttribute('href');
    link.setAttribute('aria-disabled', 'true');
    return;
  }

  link.href = viewUrl({ ...view, page });
  link.removeAttribute('aria-disabled');
}
