// An order of the dashboard and the list, from a card of GET /api/v1/orders or of the dashboard: a
// card, one <template id="order-card"> in both pages, a row of the list's table view,
// <template id="order-row">, or a card of its Pipeline layout, <template id="pipeline-card">. All
// word the same facts the same way.

import { ORDER_TYPE, STATUS } from './codes.js';
import * as format from './format.js';
import { cloneTemplate, fieldsOf, fillCreatedBy } from './shell.js';

// Clicks on these keep their own meaning instead of opening the order.
const OWN_CLICK = 'details, a, button, [aria-disabled]';

function orderUrl(number) {
  return `order.html?${new URLSearchParams({ n: number })}`;
}

// A card cannot be wrapped in a link around its <details>, nor a table row in a link at all, so one
// listener opens the order.
export function openOrdersOnClick(container) {
  container.addEventListener('click', event => {
    const order = event.target.closest('.order-card, .order-row, .pipeline-card');
    if (order && !event.target.closest(OWN_CLICK)) {
      location.assign(order.dataset.href);
    }
  });
}

// openAlertRefs: the dashboard opens a Ref N list that has an open Alert, so the alert is seen at
// once. The list leaves it closed: an open list is wider than a third of the page.
export function renderCard(order, { openAlertRefs = false } = {}) {
  const fragment = cloneTemplate('order-card');
  const card = fragment.querySelector('.order-card');
  const field = fieldsOf(card);
  const { isDraft, hasAlert } = fillOrder(card, field, order);

  card.classList.toggle('has-alert', hasAlert);
  fillRefs(field, order.shipments, openAlertRefs);
  fillCreatedBy(field, order.createdBy, 'card');
  fillTrailers(field, order);
  field(isDraft ? 'order-icons' : 'draft-icons').remove();
  return fragment;
}

// The list's table view: the orders as rows of one table, in the same order.
export function renderTable(orders) {
  const table = cloneTemplate('order-table');
  fieldsOf(table)('rows').append(...orders.map(renderRow));
  return table;
}

function renderRow(order) {
  const fragment = cloneTemplate('order-row');
  const row = fragment.querySelector('.order-row');
  const field = fieldsOf(row);
  fillOrder(row, field, order);

  // The cell may cut a long Ref N; its title keeps it whole.
  const ref = field('ref');
  ref.textContent = refSummary(order.shipments);
  ref.title = ref.textContent;
  ref.classList.toggle('is-empty', order.shipments.length === 0);
  return fragment;
}

// The compact card of a board column: the number, type, hub, date, the declared and actual totals,
// the open Alerts and the next step. Only a draft keeps the Submit button; the board wires it.
export function renderPipelineCard(order) {
  const fragment = cloneTemplate('pipeline-card');
  const card = fragment.querySelector('.pipeline-card');
  const field = fieldsOf(card);
  const isDraft = order.status === STATUS.Draft;
  const href = orderUrl(order.number);

  card.dataset.href = href;
  card.dataset.number = order.number;
  card.classList.toggle('is-draft', isDraft);
  card.classList.toggle('has-alert', order.openAlertTypes.length > 0);

  const number = field('number');
  number.href = href;
  number.textContent = order.number;
  field('alerts').textContent = format.openAlertsText(order.openAlertTypes) ?? '';
  field('type-line').textContent = format.typeLine(order);
  field('hub').textContent = format.hubShortLabel(order.hub, order.transferHub);
  field('date').textContent = format.formatDayTime(order.scheduledAt, order.hub.timeZone);
  const quantity = field('quantity');
  quantity.textContent = format.declaredAndActual(order);
  quantity.classList.toggle('qty-actual', order.hasQuantityDelta);
  fillNext(field('next'), order.nextStep, href);

  const submit = field('submit');
  if (isDraft) {
    submit.setAttribute('aria-label', format.submitLabel(order.number));
  } else {
    submit.remove();
  }

  return fragment;
}

// One Ref N as it is, several as their count.
function refSummary(shipments) {
  return shipments.length > 1
    ? format.refCount(shipments.length)
    : shipments[0]?.refNumber ?? format.DASH;
}

// What a card and a row show alike: the number as a link, the type line, the badges, Hub, Date,
// Q-ty, Carrier, Destination and the next step; and the whole element opens the order.
function fillOrder(element, field, order) {
  const isDraft = order.status === STATUS.Draft;
  const hasAlert = order.openAlertTypes.length > 0;
  const href = orderUrl(order.number);

  element.dataset.href = href;
  element.classList.toggle('is-draft', isDraft);

  const number = field('number');
  number.href = href;
  number.textContent = order.number;
  field('type-line').textContent = format.typeLine(order);
  fillBadges(field, order, isDraft, hasAlert);

  field('hub').textContent = format.hubShortLabel(order.hub, order.transferHub);
  field('date').textContent = format.formatDayTime(order.scheduledAt, order.hub.timeZone);
  fillQuantity(field('quantity'), order);
  field('carrier').textContent = format.transportLabel(order.transport);
  field('destination').textContent = format.destinationLabel(order.destination);
  fillNext(field('next'), order.nextStep, href);
  return { isDraft, hasAlert };
}

function fillRefs(field, shipments, openAlertRefs) {
  if (shipments.length === 0) {
    field('ref-single').remove();
    field('ref-multi').remove();
    return;
  }

  if (shipments.length === 1) {
    field('ref').textContent = shipments[0].refNumber ?? format.DASH;
    field('ref-multi').remove();
    return;
  }

  field('ref-single').remove();
  field('ref-count').textContent = format.refCount(shipments.length);
  field('ref-list').append(...shipments.map(renderRefItem));
  field('ref-multi').open = openAlertRefs
    && shipments.some(shipment => shipment.openAlertTypes.length > 0);
}

function renderRefItem(shipment) {
  const item = cloneTemplate('ref-item');
  item.querySelector('.sub-id').textContent = shipment.label;
  item.querySelector('.sub-ref').textContent = shipment.refNumber ?? format.DASH;
  item.querySelector('.sub-desc').prepend(format.palletCount(shipment));
  const alerts = item.querySelector('em');
  const alertsText = format.openAlertsText(shipment.openAlertTypes);
  if (alertsText) {
    alerts.before(' · ');
    alerts.textContent = alertsText;
  } else {
    alerts.remove();
  }

  return item;
}

// A draft shows its status badge alone, without the type badge.
function fillBadges(field, order, isDraft, hasAlert) {
  const typeBadge = field('type-badge');
  if (isDraft) {
    typeBadge.remove();
  } else {
    typeBadge.textContent = format.typeLabel(order.type);
    typeBadge.classList.add(format.typeBadge(order.type));
  }

  const status = format.cardStatus(order.status, hasAlert);
  const statusBadge = field('status-badge');
  statusBadge.textContent = status.label;
  statusBadge.classList.add(status.badge);
}

function fillQuantity(cell, order) {
  if (!order.hasQuantityDelta) {
    cell.textContent = format.cargoQuantity(order.declared);
    return;
  }

  const parts = format.quantityDelta(order);
  const actual = document.createElement('span');
  actual.className = 'qty-actual';
  actual.textContent = parts.actual;
  cell.replaceChildren(parts.declared, actual);
}

// Trailers only on a Consolidation that has loaded some; otherwise Destination takes the whole row.
function fillTrailers(field, order) {
  if (order.type === ORDER_TYPE.Consolidation && order.outboundTrailerCount > 0) {
    field('trailers').textContent = format.consolidatedTrailers(order.outboundTrailerCount);
    return;
  }

  field('trailers-cell').remove();
  field('destination-cell').classList.add('oc-span-2');
}

function fillNext(line, next, href) {
  const step = format.nextStep(next);
  line.classList.toggle('next-alert', step.tone === 'alert');
  line.classList.toggle('next-edit', step.tone === 'edit');
  if (step.tone === 'edit') {
    const link = document.createElement('a');
    link.href = href;
    link.textContent = step.text;
    line.replaceChildren(link);
    return;
  }

  line.textContent = step.text;
  if (step.invoice) {
    const invoice = document.createElement('span');
    invoice.className = 'next-invoice';
    invoice.textContent = step.invoice;
    line.append(' · ', invoice);
  }
}
