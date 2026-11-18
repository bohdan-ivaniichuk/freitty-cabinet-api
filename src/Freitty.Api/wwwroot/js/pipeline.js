// The Pipeline layout of orders.html: the columns of GET /api/v1/orders/pipeline side by side, one
// per status. The only move a client makes is a draft to Ready, so only a draft card can be dragged
// and only the Ready column takes it; dragging needs a mouse, so the draft's Submit button does the
// same. What a move does is the page's: onSubmit(order).

import { STATUS } from './codes.js';
import { renderPipelineCard } from './card.js';
import * as format from './format.js';
import { cloneTemplate, fieldsOf, stateBox } from './shell.js';

const DROP_TARGET = 'is-drop-target';
const DRAGGING = 'is-dragging';

// The draft being dragged; null between drags. A drag from another page or another window carries
// none, so the Ready column refuses it.
let draggedOrder = null;

// moreHref(status): the address of the list behind a column that has more orders than cards.
export function renderBoard(columns, { moreHref, onSubmit }) {
  const fragment = cloneTemplate('pipeline-board');
  const board = fragment.querySelector('.pipeline-board');
  board.append(...columns.map(column => renderColumn(column, moreHref, onSubmit)));
  return fragment;
}

// After a submit the board is new and the focus has fallen to the page: it goes to the number of
// the submitted order when its card is shown, otherwise to the heading of the Ready column.
export function focusSubmitted(container, number) {
  const card = [...container.querySelectorAll('.pipeline-card')]
    .find(candidate => candidate.dataset.number === number);
  const readyHeading = container.querySelector(
    `[data-status="${STATUS.Ready}"] .pipeline-column-head`,
  );
  (card?.querySelector('.oc-num') ?? readyHeading)?.focus();
}

export function cardCount(columns) {
  return columns.reduce((count, column) => count + column.orders.length, 0);
}

export function orderCount(columns) {
  return columns.reduce((count, column) => count + column.total, 0);
}

function renderColumn(column, moreHref, onSubmit) {
  const fragment = cloneTemplate('pipeline-column');
  const section = fragment.querySelector('.pipeline-column');
  const field = fieldsOf(section);
  const label = format.statusLabel(column.status);

  section.dataset.status = column.status;
  section.setAttribute('aria-label', label);
  field('label').textContent = label;
  field('total').textContent = column.total;
  field('cards').append(...renderCards(column, moreHref, onSubmit));

  if (column.status === STATUS.Ready) {
    acceptDrafts(section, onSubmit);
  }

  return fragment;
}

function renderCards(column, moreHref, onSubmit) {
  if (column.orders.length === 0) {
    return [stateBox(format.NO_ORDERS_IN_COLUMN)];
  }

  const cards = column.orders.map(order => renderCard(order, onSubmit));
  const hiddenCount = column.total - column.orders.length;
  if (hiddenCount > 0) {
    const more = document.createElement('a');
    more.className = 'pipeline-more';
    more.href = moreHref(column.status);
    more.dataset.moreStatus = column.status;
    more.textContent = format.moreOrders(hiddenCount);
    cards.push(more);
  }

  return cards;
}

function renderCard(order, onSubmit) {
  const fragment = renderPipelineCard(order);
  if (order.status !== STATUS.Draft) {
    return fragment;
  }

  const card = fragment.querySelector('.pipeline-card');
  card.draggable = true;
  card.addEventListener('dragstart', event => startDrag(event, card, order));
  card.addEventListener('dragend', () => endDrag(card));
  fieldsOf(card)('submit').addEventListener('click', () => onSubmit(order));
  return fragment;
}

// Firefox starts a drag only when it carries data.
function startDrag(event, card, order) {
  draggedOrder = order;
  event.dataTransfer.setData('text/plain', order.number);
  event.dataTransfer.effectAllowed = 'move';
  card.classList.add(DRAGGING);
  readyColumnOf(card)?.classList.add(DROP_TARGET);
}

// Called whether the drop landed or not; a drop outside the Ready column has no handler and does
// nothing.
function endDrag(card) {
  draggedOrder = null;
  card.classList.remove(DRAGGING);
  readyColumnOf(card)?.classList.remove(DROP_TARGET);
}

function readyColumnOf(card) {
  return card.closest('.pipeline-board')?.querySelector(`[data-status="${STATUS.Ready}"]`) ?? null;
}

// A drop is allowed only where dragover is cancelled, and only while a draft of this board is
// dragged.
function acceptDrafts(column, onSubmit) {
  const allowDrop = event => {
    if (draggedOrder) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'move';
    }
  };
  column.addEventListener('dragenter', allowDrop);
  column.addEventListener('dragover', allowDrop);
  column.addEventListener('drop', event => {
    const order = draggedOrder;
    if (order) {
      event.preventDefault();
      onSubmit(order);
    }
  });
}
