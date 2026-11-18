// orders.html: the list of GET /api/v1/orders with its tabs, filters, search and pages, as cards or
// as a table. The view lives in the URL. A change of it rewrites the URL through the History API and
// loads the counts and the list again in place, so Back, Forward and a copied link all lead to the
// same view. "Export CSV" downloads the list of the view shown, all its pages.

import { apiUrl, getJson } from './api.js';
import { openOrdersOnClick, renderCard, renderTable } from './card.js';
import { ORDER_TYPE, STATUS } from './codes.js';
import * as format from './format.js';
import {
  describeError,
  errorBox,
  initShell,
  isPlainClick,
  loadMe,
  markCurrent,
  searchText,
  showSearchQuery,
  stateBox,
} from './shell.js';

// Each tab is a filter of the list, and a field of GET /api/v1/orders/counts.
const TABS = {
  all: { parameters: {}, count: 'all' },
  crossdock: { parameters: { type: ORDER_TYPE.CrossDock }, count: 'crossDock' },
  consolidation: { parameters: { type: ORDER_TYPE.Consolidation }, count: 'consolidation' },
  alerts: { parameters: { hasOpenAlerts: 'true' }, count: 'alerts' },
  drafts: { parameters: { status: STATUS.Draft }, count: 'drafts' },
};
const DEFAULT_TAB = 'all';
// The URL's `view`: how the list is shown.
const LAYOUTS = { cards: 'cards', table: 'table' };
const DEFAULT_LAYOUT = LAYOUTS.cards;
// The filters' URL values and the API values they stand for.
const DATE_FILTERS = { today: 'Today', week: 'ThisWeek', last30: 'Last30Days' };
const STATUS_FILTERS = {
  ready: STATUS.Ready,
  inprogress: STATUS.InProgress,
  completed: STATUS.Completed,
};
const FILTERS = ['hub', 'date', 'status'];
const FIRST_PAGE = 1;
const MAX_PAGE = 10000;

let view = readView(new URLSearchParams(location.search));
// The hubs of the Hub filter; null until they are loaded, and when they could not be.
let hubs = null;
// Only the answer to the latest request is shown: an earlier one may arrive after it.
let latestCounts = 0;
let latestList = 0;

initShell({
  query: view.search,
  onSearch: search => show({ ...view, search, page: FIRST_PAGE }),
  onRestore: () => {
    loadMe();
    render();
  },
});
loadMe();
openOrdersOnClick(document.getElementById('orders'));
bindControls();
start();

// A value the page does not know is treated as absent: sent on, the API would answer 400. The hub is
// checked once the hubs are loaded.
function readView(url) {
  const known = (values, name) => (Object.hasOwn(values, url.get(name)) ? url.get(name) : null);
  const page = Number(url.get('page'));
  return {
    tab: known(TABS, 'tab') ?? DEFAULT_TAB,
    layout: known(LAYOUTS, 'view') ?? DEFAULT_LAYOUT,
    hub: url.get('hub'),
    date: known(DATE_FILTERS, 'date'),
    status: known(STATUS_FILTERS, 'status'),
    search: searchText(url.get('search')),
    page: Number.isInteger(page) && page >= FIRST_PAGE && page <= MAX_PAGE ? page : FIRST_PAGE,
  };
}

function viewUrl(next) {
  const url = new URLSearchParams();
  if (next.tab !== DEFAULT_TAB) {
    url.set('tab', next.tab);
  }

  if (next.layout !== DEFAULT_LAYOUT) {
    url.set('view', next.layout);
  }

  for (const name of [...FILTERS, 'search']) {
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

// The tabs and the filters are shown at once; only the hub waits for the hubs, and so do the counts
// and the list, which send it. The address is rewritten to the view the page really shows.
async function start() {
  renderTabs();
  renderLayouts();
  renderFilters();
  hubs = await loadHubs();
  view = withKnownHub(view);
  history.replaceState(null, '', viewUrl(view));
  render();
}

function withKnownHub(candidate) {
  const isKnown = hubs?.some(hub => hub.id === candidate.hub) ?? false;
  return isKnown ? candidate : { ...candidate, hub: null };
}

// A view the user asked for: a new entry in the history, then the view itself. Asking for the view
// already shown loads it again without a second entry.
function show(next) {
  const url = viewUrl(next);
  if (url !== viewUrl(view)) {
    history.pushState(null, '', url);
  }

  if (next.page !== view.page) {
    window.scrollTo(0, 0);
  }

  view = next;
  render();
}

function render() {
  renderTabs();
  renderLayouts();
  renderFilters();
  renderExport();
  showSearchQuery(view.search);
  loadCounts();
  loadList();
}

function bindControls() {
  for (const name of FILTERS) {
    const select = document.getElementById(`filter-${name}`);
    select.addEventListener('change', () =>
      show({ ...view, [name]: select.value || null, page: FIRST_PAGE }));
  }

  for (const tab of document.querySelectorAll('[data-tab]')) {
    showOnClick(tab, () => ({ ...view, tab: tab.dataset.tab, page: FIRST_PAGE }));
  }

  for (const link of document.querySelectorAll('[data-layout]')) {
    showOnClick(link, () => ({ ...view, layout: link.dataset.layout }));
  }

  for (const link of [document.getElementById('page-prev'), document.getElementById('page-next')]) {
    showOnClick(link, () => ({ ...view, page: Number(link.dataset.page) }));
  }

  window.addEventListener('popstate', () => {
    view = withKnownHub(readView(new URLSearchParams(location.search)));
    render();
  });
}

// A plain click on a link of the page is answered in place; the href stays for a new tab and for
// copying the address.
function showOnClick(link, nextView) {
  link.addEventListener('click', event => {
    if (link.hasAttribute('href') && isPlainClick(event)) {
      event.preventDefault();
      show(nextView());
    }
  });
}

// Without the hubs the Hub filter cannot be shown, and the list is loaded without it.
async function loadHubs() {
  const select = document.getElementById('filter-hub');
  try {
    const hubs = await getJson('/api/v1/hubs');
    select.append(...hubs.map(hub => new Option(hub.name, hub.id)));
    return hubs;
  } catch (error) {
    select.options[0].textContent = format.HUB_FILTER_UNAVAILABLE;
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
    markCurrent(tab, isActive);
  }
}

// A switch of the layout keeps everything else, the page included.
function renderLayouts() {
  for (const link of document.querySelectorAll('[data-layout]')) {
    link.href = viewUrl({ ...view, layout: link.dataset.layout });
    markCurrent(link, link.dataset.layout === view.layout);
  }
}

// The Hub filter is set once its options are loaded. Drafts is a status of its own: on that tab the
// Status filter does not apply.
function renderFilters() {
  if (hubs !== null) {
    document.getElementById('filter-hub').value = view.hub ?? '';
  }

  document.getElementById('filter-date').value = view.date ?? '';
  const status = document.getElementById('filter-status');
  status.value = view.status ?? '';
  status.disabled = view.tab === 'drafts';
}

function filterParameters() {
  return {
    hubId: view.hub,
    datePreset: DATE_FILTERS[view.date],
    status: STATUS_FILTERS[view.status],
    search: view.search,
  };
}

// The filters of the list: those of the counts, and the tab's. Drafts sends its own status.
function listParameters() {
  return { ...filterParameters(), ...TABS[view.tab].parameters };
}

// The export takes the filters of the list, without its page.
function renderExport() {
  document.getElementById('export-csv').href = apiUrl('/api/v1/orders/export', listParameters()).href;
}

async function loadCounts() {
  const countOf = tab => document.querySelector(`[data-tab="${tab}"] .count`);
  const failure = document.getElementById('counts-error');
  const request = ++latestCounts;
  try {
    const counts = await getJson('/api/v1/orders/counts', filterParameters());
    if (request !== latestCounts) {
      return;
    }

    for (const [tab, { count }] of Object.entries(TABS)) {
      countOf(tab).textContent = counts[count];
    }

    failure.hidden = true;
  } catch (error) {
    if (request !== latestCounts) {
      return;
    }

    for (const tab of Object.keys(TABS)) {
      countOf(tab).textContent = format.DASH;
    }

    failure.replaceChildren(errorBox(error));
    failure.hidden = false;
  }
}

async function loadList() {
  const list = document.getElementById('orders');
  const parameters = { ...listParameters(), page: view.page > FIRST_PAGE ? view.page : null };
  const { layout } = view;
  const request = ++latestList;
  list.setAttribute('aria-busy', 'true');
  try {
    const result = await getJson('/api/v1/orders', parameters);
    if (request !== latestList) {
      return;
    }

    list.replaceChildren(...renderItems(result.items, layout));
    renderPager(result);
  } catch (error) {
    if (request !== latestList) {
      return;
    }

    list.replaceChildren(errorBox(error));
  }

  list.removeAttribute('aria-busy');
}

function renderItems(orders, layout) {
  if (orders.length === 0) {
    return [stateBox(format.NO_MATCHING_ORDERS)];
  }

  return layout === LAYOUTS.table ? [renderTable(orders)] : orders.map(order => renderCard(order));
}

// A page past the end shows nothing, with Prev leading back to the last page.
function renderPager({ items, page, pageSize, total }) {
  const from = (page - 1) * pageSize + 1;
  document.getElementById('showing').textContent = format.showingText(from, items.length, total);

  const lastPage = Math.max(FIRST_PAGE, Math.ceil(total / pageSize));
  const previous = page > FIRST_PAGE ? Math.min(page - 1, lastPage) : null;
  setPageLink(document.getElementById('page-prev'), previous);
  setPageLink(document.getElementById('page-next'), page < lastPage ? page + 1 : null);
}

function setPageLink(link, page) {
  if (page === null) {
    link.removeAttribute('href');
    link.setAttribute('aria-disabled', 'true');
    return;
  }

  link.href = viewUrl({ ...view, page });
  link.dataset.page = page;
  link.removeAttribute('aria-disabled');
}
