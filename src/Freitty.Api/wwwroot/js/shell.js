// The frame every page shares: the sidebar and the top bar, filled from GET /api/v1/me, the search
// box, the loading, empty and error states the blocks of a page show, and the lookup of the
// data-field elements of a cloned template.

import { ApiError, getJson } from './api.js';
import { DASH, formatMoney } from './format.js';

const SEARCH_MIN_LENGTH = 3;
const SEARCH_MAX_LENGTH = 50;
const SEARCH_TOO_SHORT = `Type at least ${SEARCH_MIN_LENGTH} characters`;
const PAGE_FAILURE = {
  title: 'Something went wrong',
  detail: 'This part of the page could not be shown. Reload the page; if it happens again, '
    + 'the browser console has the details.',
};

// A search the API accepts, or null: shorter or longer text would be a 400.
export function searchText(value) {
  const text = (value ?? '').trim();
  return text.length >= SEARCH_MIN_LENGTH && text.length <= SEARCH_MAX_LENGTH ? text : null;
}

function listSearchUrl(text) {
  return text ? `orders.html?${new URLSearchParams({ q: text })}` : null;
}

// Enter in the search box navigates to searchUrl(text), text being null for an empty box. The
// list passes its own URL builder; elsewhere a search opens the list, and an empty one does nothing.
export function initShell({ query = '', searchUrl = listSearchUrl } = {}) {
  const input = document.getElementById('search');
  input.value = query;
  input.addEventListener('input', () => input.setCustomValidity(''));
  input.addEventListener('keydown', event => {
    if (event.key !== 'Enter' || event.isComposing) {
      return;
    }

    const typed = input.value.trim();
    if (typed.length > 0 && typed.length < SEARCH_MIN_LENGTH) {
      input.setCustomValidity(SEARCH_TOO_SHORT);
      input.reportValidity();
      return;
    }

    const url = searchUrl(searchText(typed));
    if (url) {
      location.assign(url);
    }
  });
}

// Fills the shell and returns the response, or null after showing the failure in the top bar.
export async function loadMe() {
  try {
    const me = await getJson('/api/v1/me');
    renderMe(me);
    return me;
  } catch (error) {
    renderMeError(error);
    return null;
  }
}

function renderMe(me) {
  const badge = document.getElementById('nav-attention');
  badge.textContent = me.needAttention;
  badge.hidden = me.needAttention === 0;
  document.getElementById('balance').textContent = formatMoney(me.company.balance);
  document.getElementById('user-initials').textContent = me.user.initials;
  document.getElementById('user-name').textContent = me.user.name;
}

function renderMeError(error) {
  const { title, detail } = describeError(error);
  document.getElementById('balance').textContent = DASH;
  const name = document.getElementById('user-name');
  name.textContent = `⚠ ${title}`;
  name.title = detail;
}

export function stateBox(message) {
  const box = document.createElement('div');
  box.className = 'state';
  box.textContent = message;
  return box;
}

export function errorBox(error) {
  const { title, detail } = describeError(error);
  const box = document.createElement('div');
  box.className = 'state state-error';
  const heading = document.createElement('strong');
  heading.textContent = title;
  box.append(heading, detail);
  return box;
}

// What a failure says to the user. The API's ProblemDetails is shown as it is; anything else is a
// bug in a page, logged with its stack for the developer and worded as one fixed sentence.
export function describeError(error) {
  if (error instanceof ApiError) {
    return { title: error.title, detail: error.detail };
  }

  console.error(error);
  return PAGE_FAILURE;
}

// The elements of a cloned template are found by their data-field name.
export function fieldsOf(root) {
  return name => root.querySelector(`[data-field="${name}"]`);
}
