// The server sends facts; this module words them. Codes become labels, instants become hub-local
// dates, numbers become money and chart coordinates, and the pages' fixed texts are named here.
// Nothing here touches the DOM.

import { ORDER_TYPE, PERIOD, PHOTO_UPLOAD, ROLE, STATUS, TRANSPORT_KIND } from './codes.js';

export const DASH = '—';

export const NO_ACTIVE_ORDERS = 'No active orders';
export const NO_MATCHING_ORDERS = 'No orders match these filters';
export const NO_ORDERS_IN_COLUMN = 'No orders';
export const NO_SHIPMENTS = 'No shipments yet';
export const NO_OPERATIONS = 'No operations yet';
export const NO_SUPPLIES = 'No supplies';
export const CHART_NO_DATA = 'No data';
export const HUB_FILTER_UNAVAILABLE = 'Hub: unavailable';
export const ORDER_NOT_ACTIVE = 'The order is not active';
export const OUT_OF_SCOPE = 'Not part of this assignment';
export const NO_PHOTOS = 'No photos yet';
export const CHOOSE_PHOTOS = 'Choose at least one photo.';
export const MISSING_PHOTO_CLOSED = 'Missing photo alert closed';
export const LOADING = 'Loading…';

const BYTES_PER_MB = 1024 * 1024;
const MAX_PHOTO_SIZE = `${PHOTO_UPLOAD.maxSizeBytes / BYTES_PER_MB} MB`;
export const PHOTO_LIMITS =
  `Up to ${PHOTO_UPLOAD.maxFiles} JPEG, PNG or WebP files, at most ${MAX_PHOTO_SIZE} each`;

const MINUS = '−';
const DATE_LOCALE = 'en-GB';
const TODAY_LOCALE = 'en-CA';
const TIME = { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' };
const MONEY = new Intl.NumberFormat('en-CA', { style: 'currency', currency: 'CAD' });

const STATUS_LABELS = {
  Draft: 'Draft',
  Ready: 'New',
  InProgress: 'In progress',
  Completed: 'Completed',
};

const STATUS_BADGES = {
  Draft: 'badge-draft',
  Ready: 'badge-new',
  InProgress: 'badge-prog',
  Completed: 'badge-done',
};

const TYPE_LABELS = { CrossDock: 'Cross-Dock', Consolidation: 'Consolidation' };

const TYPE_BADGES = { CrossDock: 'badge-simple', Consolidation: 'badge-consol' };

const CROSS_DOCK_KIND_LABELS = { Storage: 'Storage', Pickup: 'Pickup' };

const OPERATION_LABELS = {
  Unloading: 'Unloading',
  Disposal: 'Disposal',
  Restack: 'Restack',
  Loading: 'Loading',
};

const TRAILER_TYPE_LABELS = {
  DryVan53: 'Van · 53ft',
  Reefer53: 'Reefer · 53ft',
  Flatbed48: 'Flatbed · 48ft',
};

const ALERT_LABELS = { QuantityDelta: 'quantity delta', MissingPhoto: 'missing photo' };

// The Need Attention tile says "photo missing" where the other places say "missing photo".
const ATTENTION_ALERT_LABELS = { QuantityDelta: 'quantity delta', MissingPhoto: 'photo missing' };

// The "by" chip's role tag: a card writes the role out, the detail page writes a short tag.
const ROLE_TAGS = {
  card: { Admin: 'Admin', Dispatcher: 'Dispatcher' },
  detail: { Admin: 'ADMIN', Dispatcher: 'DISP' },
};

// An Admin's tag is purple and anyone else's blue. A card's .role is purple until it is marked
// .disp; the detail page colours its tag with the badge classes of the same two colours.
const ROLE_TAG_CLASSES = {
  card: { admin: null, other: 'disp' },
  detail: { admin: 'badge-consol', other: 'badge-prog' },
};

// One Unit Type is worded differently in each place it appears.
const UNIT_LABELS = {
  Standard: {
    single: 'Standard',
    mixed: 'Std',
    meta: 'Std · 48×40',
    cargo: 'Standard · 48×40',
    operation: 'Standard (48×40)',
  },
  ExtraLarge: { single: 'XL', mixed: 'XL', meta: 'XL', cargo: 'XL', operation: 'XL' },
};

const STAGES = {
  Draft: { label: 'Draft', badge: 'badge-draft' },
  AwaitingTruck: { label: 'Waiting for truck', badge: 'badge-new' },
  Unloading: { label: 'Unloading in progress', badge: 'badge-prog' },
  Loading: { label: 'Loading in progress', badge: 'badge-prog' },
  AwaitingCompletion: { label: 'Loaded', badge: 'badge-prog' },
  Completed: { label: 'Completed', badge: 'badge-done' },
};

const NEXT_STEPS = {
  ContinueEditing: { text: 'Continue editing →', tone: 'edit' },
  AwaitingTruck: { text: 'Next: Waiting for truck', tone: null },
  Unloading: { text: 'Next: Unloading', tone: null },
  Loading: { text: 'Next: Loading', tone: null },
  AwaitingCompletion: { text: 'Next: Completion', tone: null },
  UploadPhoto: { text: '⚠ Next: Upload photo', tone: 'alert' },
  ReviewQuantityDelta: { text: '⚠ Next: Review quantity', tone: 'alert' },
  Closed: { text: 'Closed', tone: null },
};

const INVOICED_STEPS = { AwaitingPayment: 'Next: Payment', Paid: 'Paid' };

const PERIOD_WORDS = { Day: 'day', Week: 'week', Month: 'month', Quarter: 'quarter' };

// An unknown code shows as itself: a new value on the server never renders as "undefined".
function labelOf(labels, code) {
  return labels[code] ?? code;
}

export function statusLabel(status) {
  return labelOf(STATUS_LABELS, status);
}

function statusBadge(status) {
  return STATUS_BADGES[status] ?? 'badge-done';
}

// A card's status badge. An open Alert takes the place of the status.
export function cardStatus(status, hasAlert) {
  return hasAlert
    ? { label: 'Alert', badge: 'badge-alert' }
    : { label: statusLabel(status), badge: statusBadge(status) };
}

export function typeLabel(type) {
  return labelOf(TYPE_LABELS, type);
}

export function typeBadge(type) {
  return TYPE_BADGES[type] ?? 'badge-simple';
}

function crossDockKindLabel(kind) {
  return labelOf(CROSS_DOCK_KIND_LABELS, kind);
}

export function operationLabel(operationType) {
  return labelOf(OPERATION_LABELS, operationType);
}

export function trailerTypeLabel(trailerType) {
  return labelOf(TRAILER_TYPE_LABELS, trailerType);
}

function alertLabel(alertType) {
  return labelOf(ALERT_LABELS, alertType);
}

// The role tag of the "by" chip in a place, 'card' or 'detail': its text and its colour class.
export function roleTag(role, place) {
  const colour = role === ROLE.Admin ? 'admin' : 'other';
  return { text: labelOf(ROLE_TAGS[place], role), className: ROLE_TAG_CLASSES[place][colour] };
}

export function unitLabel(unitType, place) {
  return UNIT_LABELS[unitType]?.[place] ?? unitType;
}

export function stage(code) {
  return STAGES[code] ?? { label: code, badge: 'badge-prog' };
}

function plural(count, one, many) {
  return `${count} ${count === 1 ? one : many}`;
}

export function formatMoney(amount) {
  return amount === null || amount === undefined ? DASH : MONEY.format(amount);
}

export function formatSigned(value) {
  if (value > 0) {
    return `+${value}`;
  }

  return value < 0 ? `${MINUS}${-value}` : '0';
}

// --- Dates. Instants are shown in the zone the response names; a bucket's start is a calendar date.

// en-GB writes September "Sept"; every month is cut to the three letters of the others.
const MONTH_LENGTH = 3;

function formatInstant(instant, options) {
  return new Intl.DateTimeFormat(DATE_LOCALE, options)
    .formatToParts(new Date(instant))
    .map(part => (part.type === 'month' ? part.value.slice(0, MONTH_LENGTH) : part.value))
    .join('');
}

// "12 Apr, 09:00"
export function formatDayTime(instant, timeZone) {
  return instant ? formatInstant(instant, { timeZone, day: 'numeric', month: 'short', ...TIME }) : DASH;
}

// "17 Apr 2026"
function formatDate(instant, timeZone) {
  return instant
    ? formatInstant(instant, { timeZone, day: 'numeric', month: 'short', year: 'numeric' })
    : DASH;
}

// "17 Apr · 08:55"
export function formatDayDotTime(instant, timeZone) {
  if (!instant) {
    return DASH;
  }

  const day = formatInstant(instant, { timeZone, day: 'numeric', month: 'short' });
  return `${day} · ${formatInstant(instant, { timeZone, ...TIME })}`;
}

function isToday(instant, timeZone, now = new Date()) {
  if (!instant) {
    return false;
  }

  const calendarDay = value =>
    new Intl.DateTimeFormat(TODAY_LOCALE, { timeZone, dateStyle: 'short' }).format(new Date(value));
  return calendarDay(instant) === calendarDay(now);
}

// A bucket starts on a calendar date ("2026-04-12"): read and printed in UTC, so no zone moves it
// to the day before.
export function bucketLabel(start, period) {
  const date = new Date(`${start}T00:00:00Z`);
  switch (period) {
    case PERIOD.Month:
      return formatInstant(date, { timeZone: 'UTC', month: 'short' });
    case PERIOD.Quarter:
      return `Q${Math.floor(date.getUTCMonth() / 3) + 1} ${date.getUTCFullYear()}`;
    default:
      return formatInstant(date, { timeZone: 'UTC', day: 'numeric', month: 'short' });
  }
}

function periodWord(period) {
  return labelOf(PERIOD_WORDS, period);
}

// --- The order card.

export function typeLine(card) {
  if (card.status === STATUS.Draft && card.shipments.length === 0) {
    return `${typeLabel(card.type)} · incomplete`;
  }

  if (card.type === ORDER_TYPE.Consolidation) {
    return `${typeLabel(card.type)} · ${plural(card.shipments.length, 'sub-order', 'sub-orders')}`;
  }

  return card.crossDockKind
    ? `${typeLabel(card.type)} · ${crossDockKindLabel(card.crossDockKind)}`
    : typeLabel(card.type);
}

// "10 × XL", "23 × Standard", "15 × Std + 3 × XL"
export function cargoQuantity(declared) {
  if (declared.length === 0) {
    return DASH;
  }

  const place = declared.length === 1 ? 'single' : 'mixed';
  return declared.map(entry => `${entry.quantity} × ${unitLabel(entry.unitType, place)}`).join(' + ');
}

// "20 decl · 18 actual" in two parts: the page shows the second one apart.
export function quantityDelta(card) {
  return { declared: `${card.declaredTotal} decl · `, actual: `${card.actualTotal} actual` };
}

// The actual quantity once anything was unloaded, the declared one before.
export function palletCount(shipment) {
  const quantity = shipment.actualQuantity > 0 ? shipment.actualQuantity : shipment.declaredQuantity;
  return plural(quantity, 'pallet', 'pallets');
}

// "⚠ quantity delta, missing photo", or null without an open Alert.
export function openAlertsText(alertTypes) {
  return alertTypes.length === 0 ? null : `⚠ ${alertTypes.map(alertLabel).join(', ')}`;
}

export function hubShortLabel(hub, transferHub) {
  return transferHub ? `${hub.shortName}→${transferHub.shortName}` : hub.name;
}

export function hubLabel(hub, transferHub) {
  const handling = `${hub.name} (${hub.region})`;
  return transferHub ? `${handling} → ${transferHub.name}` : handling;
}

export function transportLabel(transport) {
  switch (transport?.kind) {
    case TRANSPORT_KIND.Carrier:
      return transport.carrierName ?? DASH;
    case TRANSPORT_KIND.OwnDriver:
      return transport.driverName ? `Driver: ${transport.driverName}` : DASH;
    case TRANSPORT_KIND.SelfPickup:
      return 'Self pickup';
    default:
      return DASH;
  }
}

export function destinationLabel(destination) {
  return destination ? `${destination.city}, ${destination.region}` : DASH;
}

export function refCount(count) {
  return `${count} refs`;
}

export function consolidatedTrailers(count) {
  return `${count} consolidated`;
}

// The footer line: its text, an invoice number shown apart, and a tone for the page to style.
export function nextStep(next) {
  const invoiced = INVOICED_STEPS[next.code];
  if (invoiced) {
    return {
      text: `${invoiced} · ${formatMoney(next.amount)}`,
      invoice: `#${next.invoiceNumber}`,
      tone: null,
    };
  }

  return { ...(NEXT_STEPS[next.code] ?? { text: next.code, tone: null }), invoice: null };
}

// A stable number for a text, so a person always gets the same colour: the sum of its character
// codes. Not a hash for anything else — two texts may share a colour.
function codeSum(text) {
  return [...text].reduce((total, character) => total + character.codePointAt(0), 0);
}

// One person always gets one avatar colour, u1 to u4.
export function avatarClass(initials) {
  return `u${((codeSum(initials) + 2) % 4) + 1}`;
}

// --- The order list.

// "Showing 1–12 of 40": count items shown, the first of them numbered from, out of total.
export function showingText(from, count, total) {
  return count === 0 ? `Showing 0 of ${total}` : `Showing ${from}–${from + count - 1} of ${total}`;
}

// --- The order list's Pipeline layout.

// "Showing 13 of 71": the cards on the board, out of the orders its columns count.
export function boardShowingText(shown, total) {
  return `Showing ${shown} of ${total}`;
}

// "10 decl · 12 actual"
export function declaredAndActual(card) {
  const parts = quantityDelta(card);
  return `${parts.declared}${parts.actual}`;
}

export function moreOrders(count) {
  return `+${count} more`;
}

export function submitLabel(number) {
  return `Submit ${number}`;
}

export function submittedText(number) {
  return `${number} is ready`;
}

// --- The dashboard.

export function welcomeText(name) {
  return `Welcome, ${name} 👋`;
}

export function weekTrend(change) {
  if (change > 0) {
    return { text: `▲ ${change} this week`, tone: 'up' };
  }

  return change < 0
    ? { text: `▼ ${-change} this week`, tone: 'down' }
    : { text: 'no change this week', tone: 'flat' };
}

export function completedTrend(last, previous) {
  const change = last - previous;
  if (change > 0) {
    return { text: `▲ ${change} vs previous 30 d`, tone: 'up' };
  }

  return change < 0
    ? { text: `▼ ${-change} vs previous 30 d`, tone: 'down' }
    : { text: '⟶ same as previous 30 d', tone: 'flat' };
}

export function awaitingActionText(count) {
  return `${count} · awaiting your action`;
}

// "2 · alerts (quantity delta · {number})", from the first Alert type of the first order listed.
export function alertsText(count, alertOrders) {
  const text = `${count} · ${count === 1 ? 'alert' : 'alerts'}`;
  const first = alertOrders[0];
  if (!first || first.alertTypes.length === 0) {
    return text;
  }

  return `${text} (${labelOf(ATTENTION_ALERT_LABELS, first.alertTypes[0])} · ${first.number})`;
}

// The lines of the activity summary come in three parts: a lead, a figure in bold or null, the rest.
const COMPLETED_LEAD = '📈 ';
const SPEND_LEAD = '💰 ';

export function completedChange(summary) {
  const percent = summary.completedChangePercent;
  if (percent === null) {
    return {
      lead: COMPLETED_LEAD,
      emphasis: null,
      tone: null,
      text: `${summary.completedLast30Days} completed orders in the last 30 days`,
    };
  }

  return {
    lead: COMPLETED_LEAD,
    emphasis: `${percent < 0 ? MINUS : '+'}${Math.abs(percent)}%`,
    tone: percent < 0 ? 'down' : 'up',
    text: ' completed orders vs previous 30 days',
  };
}

export function spendSummary(spend, average) {
  return {
    lead: SPEND_LEAD,
    emphasis: formatMoney(spend),
    text: average === null ? ' spent' : ` spent · avg ${formatMoney(average)}/order`,
  };
}

export function bestBucketText(best, period) {
  const peak = formatMoney(best.spend);
  return `⭐ Best ${periodWord(period)}: ${bucketLabel(best.start, period)} (peak spend ${peak})`;
}

// --- Chart geometry, in the charts' 0 0 400 120 view box.

const CHART_LEFT = 10;
const BAR_SPAN = 380;
const BAR_FILL = 0.7;
const BAR_MAX_HEIGHT = 99;
const BAR_MIN_OPACITY = 0.3;
const LINE_SPAN = 360;
const LINE_MAX_HEIGHT = 92;
// Above the axis labels: a zero in the current bucket would otherwise draw its dot through its label.
const CHART_BASELINE = 104;
const LABEL_STEP = 3;

function round(value) {
  return Math.round(value * 100) / 100;
}

export function isFlat(values) {
  return values.every(value => value === 0);
}

// Labels sit on the last bucket and every third before it.
export function labelIndexes(count) {
  const indexes = [];
  for (let index = count - 1; index >= 0; index -= LABEL_STEP) {
    indexes.unshift(index);
  }

  return indexes;
}

// Bars for the non-zero values, and the centre of every slot for its label.
export function barChart(values) {
  const max = Math.max(0, ...values);
  const slot = BAR_SPAN / values.length;
  const width = BAR_FILL * slot;
  const slots = values.map((value, index) => ({ value, x: CHART_LEFT + index * slot }));
  return {
    bars: slots
      .filter(({ value }) => value > 0 && max > 0)
      .map(({ value, x }) => {
        const height = (BAR_MAX_HEIGHT * value) / max;
        return {
          x: round(x),
          y: round(CHART_BASELINE - height),
          width: round(width),
          height: round(height),
          opacity: round(BAR_MIN_OPACITY + ((1 - BAR_MIN_OPACITY) * value) / max),
        };
      }),
    labelXs: slots.map(({ x }) => round(x + width / 2)),
  };
}

// The points of the line, its path, the area under it, and the highest point — the later one on a
// tie, as the best bucket is.
export function lineChart(values) {
  const max = Math.max(0, ...values);
  const step = values.length > 1 ? LINE_SPAN / (values.length - 1) : 0;
  const points = values.map((value, index) => ({
    x: round(CHART_LEFT + index * step),
    y: round(CHART_BASELINE - (max > 0 ? (LINE_MAX_HEIGHT * value) / max : 0)),
  }));
  const line = points.map(({ x, y }, index) => `${index === 0 ? 'M' : 'L'} ${x},${y}`).join(' ');
  const first = points[0];
  const last = points[points.length - 1];
  const peakIndex = values.lastIndexOf(max);
  return {
    points,
    line,
    area: `${line} L ${last.x},${CHART_BASELINE} L ${first.x},${CHART_BASELINE} Z`,
    peakIndex,
  };
}

// --- The order detail.

export function orderPageTitle(number) {
  return `Freitty — Order ${number}`;
}

export function orderNotFound(number) {
  return `Order ${number} not found.`;
}

// "17 Apr 2026", with " · today" when that is today in the hub's zone.
export function scheduledDate(instant, timeZone) {
  const today = isToday(instant, timeZone);
  return { text: formatDate(instant, timeZone) + (today ? ' · today' : ''), isToday: today };
}

export function managementTitle(type) {
  return `${typeLabel(type)} management`;
}

export function subOrderCount(count) {
  return plural(count, 'sub-order', 'sub-orders');
}

export function deltasDetected(count) {
  return `${count} (q-ty)`;
}

// The sum of the known deltas of one Unit Type over the shipments.
export function unitDelta(shipments, unitType) {
  return shipments
    .flatMap(shipment => shipment.quantities)
    .filter(row => row.unitType === unitType && row.delta !== null)
    .reduce((sum, row) => sum + row.delta, 0);
}

export function hasDelta(row) {
  return row.delta !== null && row.delta !== 0;
}

export function deltaBadge(delta) {
  return `Δ ${formatSigned(delta)}`;
}

export function receipt(shipment) {
  if (shipment.receivedAt) {
    return { label: 'Received', badge: 'badge-done' };
  }

  return shipment.arrival
    ? { label: 'Receiving', badge: 'badge-prog' }
    : { label: 'Expected', badge: 'badge-new' };
}

export function truckAndTrailer(arrival) {
  return arrival ? `${arrival.truckNumber} / ${arrival.trailerNumber}` : DASH;
}

export function dockLabel(dock) {
  if (!dock) {
    return DASH;
  }

  return dock.bay ? `Dock ${dock.dock} · Bay ${dock.bay}` : `Dock ${dock.dock}`;
}

export function floorLeadLabel(dock) {
  return dock?.floorLead ? `${dock.floorLead} (floor lead)` : DASH;
}

export function dockWall(hubName) {
  return `${hubName.toUpperCase()} HUB`;
}

export function dockDoor(dock) {
  return `DOCK ${dock.dock}`;
}

export function dockAssigned(dock, timeZone) {
  return `Assigned ${formatDayTime(dock.assignedAt, timeZone)}`;
}

export function trailerBox(arrival) {
  return `🚚 ${arrival.trailerNumber}`;
}

// The one line under the dock: the first that applies.
export function dockStatus(orderStatus, shipment) {
  if (orderStatus === STATUS.Completed) {
    return { text: 'Released', trailerDocked: false };
  }

  if (!shipment.arrival) {
    return { text: 'Waiting for the truck', trailerDocked: false };
  }

  if (!shipment.receivedAt) {
    return { text: '● Trailer docked · unloading', trailerDocked: true };
  }

  const loading = shipment.quantities.some(row => row.onStock > 0);
  return { text: loading ? '● Trailer docked · loading' : '● Trailer docked', trailerDocked: true };
}

export function actualNote(shipment) {
  if (shipment.quantities.some(hasDelta)) {
    return 'Delta from BOL · read-only';
  }

  return shipment.receivedAt ? 'Matches BOL' : 'Receiving in progress';
}

export function warehouseNote(note) {
  return note ? `"${note}"` : DASH;
}

export function commentCounter(operation) {
  return `💬 ${operation.commentCount}`;
}

export function photoCounter(operation) {
  return `📷 ${operation.photoCount}`;
}

export function photoCounterLabel(operation) {
  return `Photos of this ${operationLabel(operation.type)}: ${operation.photoCount}`;
}

export function tooManyPhotos(count) {
  return `At most ${PHOTO_UPLOAD.maxFiles} photos can be uploaded at once; ${count} were chosen.`;
}

export function photoTooLarge(fileName) {
  return `${fileName} is larger than ${MAX_PHOTO_SIZE}.`;
}

export function notAPhoto(fileName) {
  return `${fileName} is not a JPEG, PNG or WebP image.`;
}

// After recording an operation with its photos: the Missing photo alert it raised for an instant was
// never shown, so the message says what happened instead.
export function operationRecordedWithPhotos(count) {
  return `Operation recorded · ${count === 1 ? '1 photo' : `${count} photos`} added`;
}

export function photosNotUploaded(reason) {
  return `The operation was recorded, but its photos were not uploaded: ${reason}`;
}

// Said inside the gallery, where the toast is behind the dialog's backdrop.
export function photosAdded(count, closedMissingPhoto) {
  const added = count === 1 ? '1 photo added' : `${count} photos added`;
  return closedMissingPhoto ? `${added}. ${MISSING_PHOTO_CLOSED}.` : `${added}.`;
}

export function orderGalleryTitle(orderNumber) {
  return `Photos of ${orderNumber}`;
}

export function operationGalleryTitle(shipmentLabel, operationType) {
  return `Photos of ${shipmentLabel} · ${operationLabel(operationType)}`;
}

// "FR001383-1 · Unloading · 17 Apr · 08:55"; the time is left out when the page does not know it.
export function photoGroupTitle(shipmentLabel, operationType, appliedAt, timeZone) {
  const parts = [shipmentLabel, operationLabel(operationType)];
  if (appliedAt) {
    parts.push(formatDayDotTime(appliedAt, timeZone));
  }

  return parts.join(' · ');
}

export function photoAlt(position, shipmentLabel, operationType) {
  return `Photo ${position} of the ${operationLabel(operationType)} of ${shipmentLabel}`;
}

export function openAlertTypes(shipment) {
  const open = shipment.alerts.filter(alert => alert.resolvedAt === null).map(alert => alert.type);
  return [...new Set(open)];
}

// Supply categories take three badge styles in turn, by their position among the distinct
// categories of the order in the order they are listed: one category keeps one style on a page, and
// neighbouring categories differ.
const SUPPLY_BADGES = ['badge-warn', 'badge-prog', 'badge-green'];

export function supplyBadge(categoryIndex) {
  return SUPPLY_BADGES[categoryIndex % SUPPLY_BADGES.length];
}

// Services alternate two badge styles.
export function serviceBadge(index) {
  return index % 2 === 0 ? 'badge-prog' : 'badge-consol';
}

export function invoiceLine(invoice, timeZone) {
  const payment = invoice.paidAt ? `paid ${formatDate(invoice.paidAt, timeZone)}` : 'unpaid';
  return `#${invoice.number} · ${formatMoney(invoice.amount)} · ${payment}`;
}
