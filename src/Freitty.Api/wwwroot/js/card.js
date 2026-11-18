// The order card of the dashboard and the list: one <template id="order-card"> in both pages,
// filled from a card of GET /api/v1/orders or of the dashboard.

import * as format from './format.js';
import { fieldsOf } from './shell.js';

// Clicks on these keep their own meaning instead of opening the order.
const OWN_CLICK = 'details, a, button, [aria-disabled]';

function orderUrl(number) {
  return `order.html?${new URLSearchParams({ n: number })}`;
}

// A card cannot be wrapped in a link around its <details>, so one listener opens the order.
export function openCardsOnClick(container) {
  container.addEventListener('click', event => {
    const card = event.target.closest('.order-card');
    if (card && !event.target.closest(OWN_CLICK)) {
      location.assign(card.dataset.href);
    }
  });
}

// openAlertRefs: a Ref N list with an open Alert starts open, as on the mockup's dashboard. The list
// leaves it closed: an open list is wider than a third of the page.
export function renderCard(order, { openAlertRefs = false } = {}) {
  const fragment = document.getElementById('order-card').content.cloneNode(true);
  const card = fragment.querySelector('.order-card');
  const field = fieldsOf(card);
  const isDraft = order.status === 'Draft';
  const hasAlert = order.openAlertTypes.length > 0;
  const href = orderUrl(order.number);

  card.dataset.href = href;
  card.classList.toggle('is-draft', isDraft);
  card.classList.toggle('has-alert', hasAlert);

  const number = field('number');
  number.href = href;
  number.textContent = order.number;
  fillRefs(field, order.shipments, openAlertRefs);
  field('type-line').textContent = format.typeLine(order);
  fillCreatedBy(field, order.createdBy);
  fillBadges(field, order, isDraft, hasAlert);

  field('hub').textContent = format.hubShortLabel(order.hub, order.transferHub);
  field('date').textContent = format.formatDayTime(order.scheduledAt, order.hub.timeZone);
  fillQuantity(field('quantity'), order);
  field('carrier').textContent = format.transportLabel(order.transport);
  field('destination').textContent = format.destinationLabel(order.destination);
  fillTrailers(field, order);

  fillNext(field('next'), order.next, href);
  field(isDraft ? 'order-icons' : 'draft-icons').remove();
  return fragment;
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
  field('ref-multi').open = openAlertRefs && shipments.some(shipment => shipment.openAlertTypes.length > 0);
}

function renderRefItem(shipment) {
  const item = document.getElementById('ref-item').content.cloneNode(true);
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

function fillCreatedBy(field, createdBy) {
  const avatar = field('initials');
  avatar.textContent = createdBy.initials;
  avatar.classList.add(format.avatarClass(createdBy.initials));
  avatar.after(createdBy.name);
  const role = field('role');
  role.textContent = createdBy.role;
  role.classList.toggle('disp', createdBy.role === 'Dispatcher');
}

// A draft shows only its status, as in the mockup; an open Alert takes the place of the status.
function fillBadges(field, order, isDraft, hasAlert) {
  const typeBadge = field('type-badge');
  if (isDraft) {
    typeBadge.remove();
  } else {
    typeBadge.textContent = format.typeLabel(order.type);
    typeBadge.classList.add(format.typeBadge(order.type));
  }

  const statusBadge = field('status-badge');
  statusBadge.textContent = hasAlert ? 'Alert' : format.statusLabel(order.status);
  statusBadge.classList.add(hasAlert ? 'badge-alert' : format.statusBadge(order.status));
}

function fillQuantity(cell, order) {
  if (!order.hasQuantityDelta) {
    cell.textContent = format.cargoQuantity(order.declared);
    return;
  }

  const actual = document.createElement('span');
  actual.className = 'qty-actual';
  actual.textContent = `${order.actualTotal} actual`;
  cell.replaceChildren(`${order.declaredTotal} decl · `, actual);
}

// Trailers only on a Consolidation that has loaded some; otherwise Destination takes the whole row.
function fillTrailers(field, order) {
  if (order.type === 'Consolidation' && order.outboundTrailers > 0) {
    field('trailers').textContent = format.consolidatedTrailers(order.outboundTrailers);
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
