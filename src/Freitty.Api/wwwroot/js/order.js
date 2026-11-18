// order.html?n={number}: GET /api/v1/orders/{number}. The whole order is rendered into #order from
// <template id="order-view">, so a refetch re-renders it in one replaceChildren. The "+ Operation"
// button of a shipment opens the dialog that records an Operation: POST
// /api/v1/orders/{number}/operations with the version the page shows.

import { getJson, postJson } from './api.js';
import { ALERT_TYPE, OPERATION_TYPE, ORDER_TYPE, STATUS, UNIT_TYPE, VERSION_CONFLICT } from './codes.js';
import * as format from './format.js';
import {
  cloneTemplate,
  describeError,
  errorBox,
  fieldsOf,
  fillCreatedBy,
  initShell,
  loadMe,
  stateBox,
} from './shell.js';

const NOT_FOUND = 404;
// The URL parser folds /api/v1/orders/. and /.. into other endpoints: no order has such a number.
// Other numbers can name an endpoint too — "counts" gets the 200 of /api/v1/orders/counts — so a
// response is also checked to be an order.
const UNREACHABLE_NUMBERS = ['.', '..'];

// The hub works only on these; on the others the "+ Operation" buttons are disabled.
const ACTIVE_STATUSES = [STATUS.Ready, STATUS.InProgress];
// These move freight through a trailer: they take its number and are billable by default.
const TRAILER_TYPES = [OPERATION_TYPE.Unloading, OPERATION_TYPE.Loading];

const number = new URLSearchParams(location.search).get('n')?.trim();
const operationDialog = document.getElementById('operation-dialog');
const operationForm = document.getElementById('operation-form');
const operationField = fieldsOf(operationForm);
const control = name => operationForm.elements.namedItem(name);

// The order as the page shows it: the dialog reads its shipments and sends its version.
let shownOrder = null;

if (number) {
  initShell({
    onRestore: () => {
      loadMe();
      loadOrder();
    },
  });
  initOperationForm();
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
    // A number such as "counts" reaches another endpoint, which answers 200 with something else.
    const isOrder = typeof order?.number === 'string';
    shownOrder = isOrder ? order : null;
    if (isOrder) {
      document.title = format.orderPageTitle(order.number);
    }

    region.replaceChildren(isOrder ? renderOrder(order) : notFound(notFoundDetail()));
  } catch (error) {
    region.replaceChildren(error.status === NOT_FOUND ? notFound(error.detail) : errorBox(error));
  }
}

function notFoundDetail() {
  return format.orderNotFound(number);
}

function notFound(detail) {
  const state = cloneTemplate('order-not-found');
  fieldsOf(state)('detail').textContent = detail;
  return state;
}

// A cell shows its value in its styled child element, or a plain dash without one.
function fillOrDash(cell, text) {
  if (text === format.DASH) {
    cell.replaceChildren(format.DASH);
  } else {
    (cell.firstElementChild ?? cell).textContent = text;
  }
}

function renderOrder(order) {
  const view = cloneTemplate('order-view');
  const field = fieldsOf(view);
  const timeZone = order.hub.timeZone;
  const isCrossDock = order.type === ORDER_TYPE.CrossDock;

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
    const empty = stateBox(format.NO_SHIPMENTS);
    empty.classList.add('shipments-empty');
    shipments.replaceChildren(empty);
  } else {
    shipments.replaceChildren(
      ...order.shipments.map(shipment => renderShipment(order, shipment, isCrossDock)));
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
    field('order-ref').textContent = order.shipments[0].refNumber ?? format.DASH;
  } else {
    field('ref-single').remove();
  }

  if (order.shipments.length > 1) {
    field('sub-orders').textContent = format.subOrderCount(order.shipments.length);
  } else {
    field('sub-orders').remove();
  }

  fillCreatedBy(field, order.createdBy, 'detail');

  field('order-delta').hidden = order.totals.quantityDeltaCount === 0;
  field('missing-photo').hidden = !order.shipments.some(shipment =>
    format.openAlertTypes(shipment).includes(ALERT_TYPE.MissingPhoto));
  field('comments').textContent = order.totals.commentCount;
  field('photos').textContent = order.totals.photoCount;
}

function renderMeta(field, order, timeZone) {
  field('customer').textContent = order.company.name;
  field('hub').textContent = format.hubLabel(order.hub, order.transferHub);
  renderServices(field('services'), order.services);

  const date = field('date');
  const scheduled = format.scheduledDate(order.scheduledAt, timeZone);
  date.textContent = scheduled.text;
  date.classList.toggle('meta-today', scheduled.isToday);

  const deltaOf = unitType => format.unitDelta(order.shipments, unitType);
  renderQuantities(field('declared-total'), order.totals.declared, () => 0);
  renderQuantities(field('actual-total'), order.totals.actual, deltaOf);
  const hasDelta = order.totals.actual.some(entry => deltaOf(entry.unitType) !== 0);
  field('actual-total').classList.toggle('meta-delta', hasDelta);

  field('carrier').textContent = format.transportLabel(order.transport);
  field('phone').textContent = order.transport?.carrierPhone ?? format.DASH;
  field('destination').textContent = format.destinationLabel(order.destination);

  for (const status of Object.values(STATUS)) {
    const step = field('meta').querySelector(`[data-status="${status}"]`);
    step.textContent = format.statusLabel(status);
    step.classList.toggle('flow-current', status === order.status);
  }

  field('deltas').textContent = format.deltasDetected(order.totals.quantityDeltaCount);
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
  const trailerType = arrival ? format.trailerTypeLabel(arrival.trailerType) : format.DASH;
  fillOrDash(fact('trailer-type'), trailerType);
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
  prepareAddOperation(field('add-operation'), order.status, shipment.position);
  return block;
}

function prepareAddOperation(button, status, position) {
  if (!ACTIVE_STATUSES.includes(status)) {
    button.title = format.ORDER_NOT_ACTIVE;
    return;
  }

  button.disabled = false;
  button.removeAttribute('title');
  button.addEventListener('click', () => openOperationDialog(position));
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
    marker.textContent = format.openAlertsText([alertType]);
    title.append(marker);
  }

  const rows = shipment.operations.map(operation => operationRow(operation, timeZone));
  field('operations').replaceChildren(...(rows.length === 0 ? [emptyRow(format.NO_OPERATIONS)] : rows));
}

function operationRow(operation, timeZone) {
  const row = cloneTemplate('operation-row');
  const field = fieldsOf(row);
  const isDisposal = operation.type === OPERATION_TYPE.Disposal;
  row.firstElementChild.classList.toggle('op-disposal', isDisposal);

  if (!operation.isBillable) {
    field('billable').remove();
  }

  // A Disposal is shown as a badge, any other type as plain text.
  const label = format.operationLabel(operation.type);
  if (isDisposal) {
    field('disposal').textContent = label;
  } else {
    field('disposal').replaceWith(label);
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
  const categories = [...new Set(supplies.lines.map(line => line.category))];
  const rows = supplies.lines.map(line => {
    const row = cloneTemplate('supply-row');
    const cell = fieldsOf(row);
    cell('sku').textContent = line.sku;
    cell('category').textContent = line.category;
    cell('category').classList.add(format.supplyBadge(categories.indexOf(line.category)));
    cell('quantity').textContent = line.quantity;
    cell('unit-price').textContent = format.formatMoney(line.unitPrice);
    cell('line-total').textContent = format.formatMoney(line.lineTotal);
    return row;
  });
  field('supplies').replaceChildren(...(rows.length === 0 ? [emptyRow(format.NO_SUPPLIES)] : rows));
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

// --- The dialog that records an Operation. It lives outside #order, so a refetch behind it keeps
// what was typed.

// The outcome of a request must land in the dialog that sent it, so while it is in flight Cancel is
// disabled and Escape is refused. A browser may still close the dialog on a second Escape: a failure
// then opens it again (recordOperation).
function initOperationForm() {
  control('type').replaceChildren(...Object.values(OPERATION_TYPE)
    .map(type => new Option(format.operationLabel(type), type)));
  control('unitType').replaceChildren(...Object.values(UNIT_TYPE)
    .map(unitType => new Option(format.unitLabel(unitType, 'operation'), unitType)));
  control('shipmentPosition').addEventListener('change', applyShipmentDefaults);
  control('type').addEventListener('change', applyTypeDefaults);
  operationField('cancel').addEventListener('click', () => operationDialog.close());
  operationDialog.addEventListener('cancel', event => {
    if (operationField('record').disabled) {
      event.preventDefault();
    }
  });
  operationForm.addEventListener('submit', recordOperation);
}

function openOperationDialog(position) {
  operationForm.reset();
  clearOperationErrors();
  const shipmentSelect = control('shipmentPosition');
  const options = shownOrder.shipments.map(shipment => new Option(shipment.label, shipment.position));
  shipmentSelect.replaceChildren(...options);
  shipmentSelect.value = String(position);
  operationField('shipment-field').hidden = shownOrder.shipments.length === 1;
  applyShipmentDefaults();
  applyTypeDefaults();
  operationDialog.showModal();
}

function selectedShipment() {
  const position = Number(control('shipmentPosition').value);
  return shownOrder.shipments.find(shipment => shipment.position === position);
}

// The trailer the shipment arrived on, when it has arrived.
function applyShipmentDefaults() {
  control('trailerNumber').value = selectedShipment()?.arrival?.trailerNumber ?? '';
}

function applyTypeDefaults() {
  const movesThroughTrailer = TRAILER_TYPES.includes(control('type').value);
  control('trailerNumber').disabled = !movesThroughTrailer;
  control('isBillable').checked = movesThroughTrailer;
}

async function recordOperation(event) {
  event.preventDefault();
  const request = operationRequest();
  setInFlight(true);
  clearOperationErrors();
  try {
    await postJson(`/api/v1/orders/${encodeURIComponent(shownOrder.number)}/operations`, request);
    operationDialog.close();
    await Promise.all([loadOrder(), loadMe()]);
    focusAddOperation(request.shipmentPosition);
  } catch (error) {
    if (!operationDialog.open) {
      operationDialog.showModal();
    }

    showOperationError(error);
    if (error.code === VERSION_CONFLICT) {
      await loadOrder();
    }
  } finally {
    setInFlight(false);
  }
}

function setInFlight(inFlight) {
  operationField('record').disabled = inFlight;
  operationField('cancel').disabled = inFlight;
}

// The button that opened the dialog was replaced by the refetch; its successor takes the focus.
function focusAddOperation(position) {
  const index = shownOrder?.shipments.findIndex(shipment => shipment.position === position) ?? -1;
  document.querySelectorAll('#order [data-field="add-operation"]')[index]?.focus();
}

// An empty number box is left out of the request, so the API names the field that is missing.
function operationRequest() {
  const trailer = control('trailerNumber');
  return {
    shipmentPosition: Number(control('shipmentPosition').value),
    type: control('type').value,
    unitType: control('unitType').value,
    quantity: numberOrAbsent(control('quantity').value),
    trailerNumber: trailer.disabled ? null : trailer.value,
    isBillable: control('isBillable').checked,
    photoCount: numberOrAbsent(control('photoCount').value),
    expectedVersion: shownOrder.version,
  };
}

function numberOrAbsent(text) {
  return text.trim() === '' ? undefined : Number(text);
}

// Each reason is shown once: a field error under its field when the form has that field, otherwise
// under the fields; a refusal without field errors shows its detail there.
function showOperationError(error) {
  const { title, detail } = describeError(error);
  const fieldErrors = error.errors ?? [];
  const unplaced = fieldErrors.filter(fieldError => !showFieldError(fieldError));
  operationField('operation-error').textContent = fieldErrors.length > 0
    ? unplaced.map(fieldError => fieldError.reason).join(' ')
    : detail || title;
}

// True when the form has a slot for the field.
function showFieldError(fieldError) {
  const slot = operationForm.querySelector(`[data-error="${CSS.escape(fieldError.name)}"]`);
  if (slot) {
    slot.textContent = fieldError.reason;
  }

  return slot !== null;
}

function clearOperationErrors() {
  operationField('operation-error').textContent = '';
  operationForm.querySelectorAll('[data-error]').forEach(slot => {
    slot.textContent = '';
  });
}
