// The frame every page shares: the sidebar and the top bar, filled from GET /api/v1/me, the search
// box, the message a control outside the assignment answers with, the loading, empty and error
// states the blocks of a page show, the cloning of a template and the lookup of its data-field
// elements, and the "by" chip of a card and of the detail page.

import { ApiError, getJson } from './api.js';
import { DASH, OUT_OF_SCOPE, avatarClass, formatMoney, roleTag } from './format.js';

const SEARCH_MIN_LENGTH = 3;
const SEARCH_MAX_LENGTH = 50;
const SEARCH_TOO_SHORT = `Type at least ${SEARCH_MIN_LENGTH} characters`;
// The controls the assignment does not cover carry this marker; a control disabled for a reason of its
// own — Prev / Next at the ends, a filter that does not apply, a request in flight — does not.
const OUT_OF_SCOPE_CONTROL = '[data-out-of-scope]';
const TOAST_DURATION = 3000;
let toastTimer = 0;
const PAGE_FAILURE = {
  title: 'Something went wrong',
  detail: 'This part of the page could not be shown. Reload the page; if it happens again, '
    + 'the browser console has the details.',
};

// A search the API accepts, or null: shorter or longer text, or text with a control character,
// would be a 400.
export function searchText(value) {
  const text = (value ?? '').trim();
  const fits = text.length >= SEARCH_MIN_LENGTH && text.length <= SEARCH_MAX_LENGTH;
  return fits && !/\p{Cc}/u.test(text) ? text : null;
}

// A search made anywhere but on the list opens the list; an empty one does nothing.
function openListSearch(text) {
  if (text) {
    location.assign(`orders.html?${new URLSearchParams({ search: text })}`);
  }
}

// A plain left click on a link, which a page may answer in place. A modified click keeps its
// meaning: a new tab, a new window, a download.
export function isPlainClick(event) {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;
}

// The link of the view that is shown, among the links that switch views in place.
export function markCurrent(link, isCurrent) {
  if (isCurrent) {
    link.setAttribute('aria-current', 'page');
  } else {
    link.removeAttribute('aria-current');
  }
}

export function showSearchQuery(text) {
  document.getElementById('search').value = text ?? '';
}

// Enter in the search box calls onSearch(text), text being null for an empty box; the list shows
// the result in place. Back can restore a page from the back/forward cache with the counters and
// statuses it showed before a write: onRestore loads them again.
export function initShell({
  query = '',
  onSearch = openListSearch,
  onRestore = () => location.reload(),
} = {}) {
  window.addEventListener('pageshow', event => {
    if (event.persisted) {
      onRestore();
    }
  });
  answerOutOfScope();

  const input = document.getElementById('search');
  showSearchQuery(query);
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

    onSearch(searchText(typed));
  });
}

// A click on a control outside the assignment says so, instead of doing nothing. A <button> turns
// Enter and Space into a click itself; any other control focused with the keyboard is answered here.
function answerOutOfScope() {
  document.addEventListener('click', event => {
    if (event.target.closest(OUT_OF_SCOPE_CONTROL)) {
      event.preventDefault();
      showToast(OUT_OF_SCOPE);
    }
  });
  document.addEventListener('keydown', event => {
    const isActivation = event.key === 'Enter' || event.key === ' ';
    if (isActivation && event.target.matches(`${OUT_OF_SCOPE_CONTROL}:not(button)`)) {
      event.preventDefault();
      showToast(OUT_OF_SCOPE);
    }
  });
}

// One message at a time: a new one replaces the one shown and starts its time again.
function showToast(message) {
  const toast = document.getElementById('toast');
  toast.textContent = message;
  toast.classList.add('is-shown');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('is-shown'), TOAST_DURATION);
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
  badge.textContent = me.needAttentionCount;
  badge.hidden = me.needAttentionCount === 0;
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

export function cloneTemplate(id) {
  return document.getElementById(id).content.cloneNode(true);
}

// The elements of a cloned template are found by their data-field name.
export function fieldsOf(root) {
  return name => root.querySelector(`[data-field="${name}"]`);
}

// The avatar with its colour, the name after it and the role tag; place is 'card' or 'detail'.
export function fillCreatedBy(field, createdBy, place) {
  const avatar = field('initials');
  avatar.textContent = createdBy.initials;
  avatar.classList.add(avatarClass(createdBy.initials));
  avatar.after(createdBy.name);

  const tag = roleTag(createdBy.role, place);
  const role = field('role');
  role.textContent = tag.text;
  if (tag.className) {
    role.classList.add(tag.className);
  }
}
