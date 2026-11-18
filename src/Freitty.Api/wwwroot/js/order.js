// order.html?n={number}: GET /api/v1/orders/{number}. The whole order is rendered into #order from
// <template id="order-view">, so a refetch re-renders it in one replaceChildren.

import { getJson } from './api.js';
import * as format from './format.js';
import { errorBox, fieldsOf, initShell, loadMe, stateBox } from './shell.js';

const NOT_FOUND = 404;
// The URL parser folds /api/v1/orders/. and /.. into other endpoints: no order has such a number.
const UNREACHABLE_NUMBERS = ['.', '..'];

const number = new URLSearchParams(location.search).get('n')?.trim();

if (number) {
  initShell();
  document.getElementById('crumbs').append(number);
  loadMe();
  loadOrder();
} else {
  location.replace('orders.html');
}

// Renders the order into #order; called again after a change, it replaces what is there.
async function loadOrder() {
  const region = document.getElementById('order');
  if (UNREACHABLE_NUMBERS.includes(number)) {
    region.replaceChildren(notFound(notFoundDetail()));
    return;
  }

  try {
    const order = await getJson(`/api/v1/orders/${encodeURIComponent(number)}`);
    const isOrder = typeof order?.number === 'string';
    region.replaceChildren(isOrder ? renderOrder(order) : notFound(notFoundDetail()));
  } catch (error) {
    region.replaceChildren(error.status === NOT_FOUND ? notFound(error.detail) : errorBox(error));
  }
}

function notFoundDetail() {
  return `Order ${number} not found.`;
}

function notFound(detail) {
  const state = cloneTemplate('order-not-found');
  state.querySelector('[data-field="detail"]').textContent = detail;
  return state;
}

function cloneTemplate(id) {
  return document.getElementById(id).content.cloneNode(true);
}

// The fields of the order itself. A shipment block has fields of the same names, so a field inside
// the shipments container is never the order's.
function orderFieldsOf(view) {
  return name => [...view.querySelectorAll(`[data-field="${name}"]`)]
    .find(element => !element.parentElement?.closest('[data-field="shipments"]'));
}

// A cell shows its value in the element the mockup styles it with, or a plain dash without one.
function fillOrDash(cell, text) {
  if (text === format.DASH) {
    cell.replaceChildren(format.DASH);
  } else {
    (cell.firstElementChild ?? cell).textContent = text;
  }
}

function renderOrder(order) {
  const view = cloneTemplate('order-view');
  const field = orderFieldsOf(view);
  const timeZone = order.hub.timeZone;
  const isCrossDock = order.type === 'CrossDock';

  field('management').textContent = format.managementTitle(order.type);
  renderHeader(field, order);
  field('transfer-banner').hidden = order.transferHub === null;
  renderMeta(field, order, timeZone);

  const meta = field('meta');
  if (isCrossDock) {
    fillShipmentFacts(meta, order.shipments[0]);
  } else {
    meta.querySelectorAll('[data-crossdock]').forEach(cell => cell.remove());
  }

  const shipments = field('shipments');
  if (order.shipments.length === 0) {
    const empty = stateBox('No shipments yet');
    empty.classList.add('shipments-empty');
    shipments.replaceChildren(empty);
  } else {
    shipments.replaceChildren(...order.shipments.map(shipment => renderShipment(order, shipment, isCrossDock)));
  }

  renderSupplies(field, order.supplies);
  renderInvoices(field, order.invoices, timeZone);
  return view;
}

function renderHeader(field, order) {
  field('number').textContent = order.number;
  field('invoiced').hidden = order.invoices.length === 0;
  field('transport').hidden = order.transport === null;
  field('type').textContent = format.typeLabel(order.type);
  field('on-stock').hidden = !order.totals.onStock.some(entry => entry.quantity > 0);

  const stage = format.stage(order.stage);
  field('stage').textContent = stage.label;
  field('stage').classList.add(stage.badge);

  if (order.shipments.length === 1) {
    field('ref').textContent = order.shipments[0].refNumber ?? format.DASH;
  } else {
    field('ref-single').remove();
  }

  if (order.shipments.length > 1) {
    field('sub-orders').textContent = format.subOrderCount(order.shipments.length);
  } else {
    field('sub-orders').remove();
  }

  const avatar = field('initials');
  avatar.textContent = order.createdBy.initials;
  avatar.classList.add(format.avatarClass(order.createdBy.initials));
  const role = field('role');
  role.before(order.createdBy.name);
  role.textContent = format.roleTag(order.createdBy.role);
  role.classList.add(order.createdBy.role === 'Admin' ? 'badge-consol' : 'badge-prog');

  field('delta').hidden = order.totals.quantityDeltas === 0;
  field('missing-photo').hidden = !order.shipments.some(shipment =>
    format.openAlertTypes(shipment).includes('MissingPhoto'));
  field('comments').textContent = order.totals.comments;
  field('photos').textContent = order.totals.photos;
}

function renderMeta(field, order, timeZone) {
  field('customer').textContent = order.company.name;
  field('hub').textContent = format.hubLabel(order.hub, order.transferHub);
  renderServices(field('services'), order.services);

  const date = field('date');
  const today = format.isToday(order.scheduledAt, timeZone);
  date.textContent = format.formatDate(order.scheduledAt, timeZone) + (today ? ' · today' : '');
  date.classList.toggle('meta-today', today);

  const deltaOf = unitType => format.unitDelta(order.shipments, unitType);
  renderQuantities(field('declared'), order.totals.declared, () => 0);
  renderQuantities(field('actual'), order.totals.actual, deltaOf);
  const hasDelta = order.totals.actual.some(entry => deltaOf(entry.unitType) !== 0);
  field('actual').classList.toggle('meta-delta', hasDelta);

  field('carrier').textContent = format.transportLabel(order.transport);
  field('phone').textContent = order.transport?.carrierPhone ?? format.DASH;
  field('destination').textContent = format.destinationLabel(order.destination);

  for (const status of format.STATUSES) {
    const step = field('meta').querySelector(`[data-status="${status}"]`);
    step.textContent = format.statusLabel(status);
    step.classList.toggle('flow-current', status === order.status);
  }

  field('deltas').textContent = format.deltasDetected(order.totals.quantityDeltas);
}

function renderServices(cell, services) {
  if (services.length === 0) {
    cell.textContent = format.DASH;
    return;
  }

  const badges = services.map((service, index) => {
    const badge = document.createElement('span');
    badge.className = `badge ${format.serviceBadge(index)}`;
    badge.textContent = service;
    return badge;
  });
  cell.replaceChildren(...badges.flatMap((badge, index) => (index === 0 ? [badge] : [' ', badge])));
}

// "10 Std · 48×40 + 2 XL", each with its delta badge when deltaOf gives one.
function renderQuantities(cell, entries, deltaOf) {
  if (entries.length === 0) {
    cell.textContent = format.DASH;
    return;
  }

  const parts = entries.flatMap((entry, index) => {
    const unit = cloneTemplate('meta-quantity').firstElementChild;
    unit.textContent = format.unitLabel(entry.unitType, 'meta');
    const part = [...(index === 0 ? [] : [' + ']), `${entry.quantity} `, unit];
    const delta = deltaOf(entry.unitType);
    if (delta !== 0) {
      const badge = cloneTemplate('meta-delta').firstElementChild;
      badge.textContent = format.deltaBadge(delta);
      part.push(' ', badge);
    }

    return part;
  });
  cell.replaceChildren(...parts);
}

// Trailer type, truck and trailer, dock and floor lead: in the meta grid of a Cross-Dock — dashes
// for a draft without its shipment — and in the header of each block of a Consolidation.
function fillShipmentFacts(root, shipment) {
  const fact = name => root.querySelector(`[data-fact="${name}"]`);
  const arrival = shipment?.arrival ?? null;
  const dock = shipment?.dock ?? null;
  fillOrDash(fact('trailer-type'), arrival ? format.trailerTypeLabel(arrival.trailerType) : format.DASH);
  fillOrDash(fact('truck'), format.truckAndTrailer(arrival));
  fillOrDash(fact('dock'), format.dockLabel(dock));
  fillOrDash(fact('floor-lead'), format.floorLeadLabel(dock));
}

function renderShipment(order, shipment, isCrossDock) {
  const block = cloneTemplate('shipment-block');
  const root = block.firstElementChild;
  const field = fieldsOf(root);
  const timeZone = order.hub.timeZone;

  if (isCrossDock) {
    field('header').remove();
  } else {
    root.classList.add('shipment-box');
    field('label').textContent = shipment.label;
    field('ref').textContent = shipment.refNumber ?? format.DASH;
    const receipt = format.receipt(shipment);
    field('receipt').textContent = receipt.label;
    field('receipt').classList.add(receipt.badge);
    fillShipmentFacts(field('header'), shipment);
  }

  renderDock(field, order, shipment);
  renderCargo(field, shipment);
  field('note').textContent = format.warehouseNote(shipment.warehouseNote);
  renderOperations(field, shipment, timeZone);
  return block;
}

function renderDock(field, order, shipment) {
  const status = format.dockStatus(order.status, shipment);
  field('dock-status').textContent = status.text;
  const { dock } = shipment;
  if (!dock) {
    field('dock-schematic').remove();
    field('dock-name').remove();
    field('dock-assigned').remove();
    return;
  }

  field('no-dock').remove();
  field('dock-wall').textContent = format.dockWall(order.hub.name);
  field('dock-door').textContent = format.dockDoor(dock);
  field('dock-name').firstElementChild.textContent = format.dockLabel(dock);
  field('dock-assigned').textContent = format.dockAssigned(dock, order.hub.timeZone);
  if (status.trailerDocked) {
    field('dock-trailer').textContent = format.trailerBox(shipment.arrival);
  } else {
    field('dock-trailer').remove();
  }
}

// One line per Unit Type in Expected and in Actual; the Actual card turns amber for a delta.
function renderCargo(field, shipment) {
  const rows = shipment.quantities;
  const note = field('actual-note');
  if (rows.length === 0) {
    field('expected').append(cargoLine(format.DASH, ''));
    note.before(actualLine(format.DASH, null, ''));
  }

  for (const row of rows) {
    const unit = format.unitLabel(row.unitType, 'cargo');
    field('expected').append(cargoLine(row.declared, unit));
    note.before(actualLine(row.actual, format.hasDelta(row) ? row.delta : null, unit));
  }

  field('actual').classList.toggle('has-delta', rows.some(format.hasDelta));
  note.textContent = format.actualNote(shipment);
}

function cargoLine(quantity, unit) {
  const line = cloneTemplate('cargo-line');
  const field = fieldsOf(line);
  field('quantity').textContent = quantity;
  field('unit').textContent = unit;
  return line;
}

function actualLine(quantity, delta, unit) {
  const line = cloneTemplate('actual-line');
  const field = fieldsOf(line);
  const badge = field('delta');
  if (delta === null) {
    badge.remove();
  } else {
    badge.textContent = format.formatSigned(delta);
  }

  field('quantity').prepend(String(quantity));
  field('unit').textContent = unit;
  return line;
}

function renderOperations(field, shipment, timeZone) {
  const title = field('operations-title');
  for (const alertType of format.openAlertTypes(shipment)) {
    const marker = document.createElement('span');
    marker.className = 'operations-alert';
    marker.textContent = `⚠ ${format.alertLabel(alertType)}`;
    title.append(marker);
  }

  const rows = shipment.operations.map(operation => operationRow(operation, timeZone));
  field('operations').replaceChildren(...(rows.length === 0 ? [emptyRow('No operations yet')] : rows));
}

function operationRow(operation, timeZone) {
  const row = cloneTemplate('operation-row');
  const field = fieldsOf(row);
  const isDisposal = operation.type === 'Disposal';
  row.firstElementChild.classList.toggle('op-disposal', isDisposal);

  if (!operation.isBillable) {
    field('billable').remove();
  }

  if (!isDisposal) {
    field('disposal').replaceWith(operation.type);
  }

  field('trailer').textContent = operation.trailerNumber ?? format.DASH;
  field('quantity').textContent = operation.quantity;
  field('unit').textContent = format.unitLabel(operation.unitType, 'operation');
  field('applied').textContent = format.formatDayDotTime(operation.appliedAt, timeZone);
  field('counters').textContent = format.operationCounters(operation);
  return row;
}

function emptyRow(message) {
  const row = cloneTemplate('empty-row');
  row.querySelector('td').textContent = message;
  return row;
}

function renderSupplies(field, supplies) {
  const rows = supplies.lines.map(line => {
    const row = cloneTemplate('supply-row');
    const cell = fieldsOf(row);
    cell('sku').textContent = line.sku;
    cell('category').textContent = line.category;
    cell('category').classList.add(format.supplyBadge(line.category));
    cell('quantity').textContent = line.quantity;
    cell('unit-price').textContent = format.formatMoney(line.unitPrice);
    cell('line-total').textContent = format.formatMoney(line.lineTotal);
    return row;
  });
  field('supplies').replaceChildren(...(rows.length === 0 ? [emptyRow('No supplies')] : rows));
  field('subtotal').textContent = format.formatMoney(supplies.subtotal);
}

function renderInvoices(field, invoices, timeZone) {
  if (invoices.length === 0) {
    field('invoices').remove();
    return;
  }

  field('invoice-lines').replaceChildren(...invoices.map(invoice => {
    const line = cloneTemplate('invoice-line').firstElementChild;
    line.textContent = format.invoiceLine(invoice, timeZone);
    return line;
  }));
}
