// index.html: GET /api/v1/dashboard for the tiles, the active orders and the activity charts.
// The period is the URL's `period`. A period button rewrites it through the History API and
// redraws the activity block in place.

import { getJson } from './api.js';
import { openCardsOnClick, renderCard } from './card.js';
import { PERIOD } from './codes.js';
import * as format from './format.js';
import { errorBox, initShell, isPlainClick, loadMe, markCurrent, stateBox } from './shell.js';

const PERIODS = { day: PERIOD.Day, week: PERIOD.Week, month: PERIOD.Month, quarter: PERIOD.Quarter };
const DEFAULT_PERIOD = 'week';
const SVG = 'http://www.w3.org/2000/svg';
const TONES = { up: 'trend-up', down: 'trend-down', flat: 'trend-flat' };
const AXIS_LABEL = { y: 117, 'font-size': 9, fill: '#94A3B8' };
const BAR = { rx: 3, fill: '#16A34A' };
const SPEND_COLOUR = '#ED1C2E';
const AREA = { fill: SPEND_COLOUR, opacity: 0.12 };
const LINE = {
  stroke: SPEND_COLOUR,
  'stroke-width': 2,
  fill: 'none',
  'stroke-linecap': 'round',
  'stroke-linejoin': 'round',
};
const DOT = { r: 3, fill: SPEND_COLOUR };
const PEAK_DOT = { r: 4, fill: SPEND_COLOUR, stroke: '#fff', 'stroke-width': 2 };
const NO_DATA = { x: 200, y: 60, 'font-size': 11, fill: '#94A3B8', 'text-anchor': 'middle' };

let periodKey = readPeriodKey();
// Only the answer to the latest request is shown: an earlier one may arrive after it.
let latestRequest = 0;

initShell({
  onRestore: () => {
    loadMe();
    loadDashboard();
  },
});
openCardsOnClick(document.getElementById('active-orders'));
bindPeriods();
markActivePeriod();
loadMe().then(renderWelcome);
loadDashboard();

// An unknown or empty period would be a 400 for the API: the page asks for the default instead.
function readPeriodKey() {
  const key = new URLSearchParams(location.search).get('period');
  return Object.hasOwn(PERIODS, key) ? key : DEFAULT_PERIOD;
}

// A plain click on a period is answered in place; the href stays for a new tab and for copying.
function bindPeriods() {
  for (const link of document.querySelectorAll('[data-period]')) {
    link.addEventListener('click', event => {
      if (!isPlainClick(event)) {
        return;
      }

      event.preventDefault();
      if (link.dataset.period !== periodKey) {
        history.pushState(null, '', link.href);
        showPeriod(link.dataset.period);
      }
    });
  }

  window.addEventListener('popstate', () => showPeriod(readPeriodKey()));
}

// The tiles and the cards do not depend on the period: only the activity block is drawn again.
function showPeriod(key) {
  periodKey = key;
  markActivePeriod();
  loadDashboard({ activityOnly: true });
}

function renderWelcome(me) {
  if (me) {
    document.getElementById('welcome').textContent = format.welcomeText(me.user.name);
  }
}

function markActivePeriod() {
  for (const link of document.querySelectorAll('[data-period]')) {
    const isActive = link.dataset.period === periodKey;
    link.classList.toggle('active', isActive);
    markCurrent(link, isActive);
  }
}

async function loadDashboard({ activityOnly = false } = {}) {
  const state = document.getElementById('dashboard-state');
  const content = document.getElementById('dashboard');
  const request = ++latestRequest;
  try {
    const dashboard = await getJson('/api/v1/dashboard', { period: PERIODS[periodKey] });
    if (request !== latestRequest) {
      return;
    }

    if (!activityOnly) {
      renderTiles(dashboard);
      renderActiveOrders(dashboard.orders);
    }

    renderActivity(dashboard.activity);
    state.hidden = true;
    content.hidden = false;
  } catch (error) {
    if (request !== latestRequest) {
      return;
    }

    state.replaceChildren(errorBox(error));
    state.hidden = false;
    content.hidden = true;
  }
}

// Drawn again when the page is restored, so the tone of the last time is replaced, not added to.
function setTrend(element, trend) {
  element.textContent = trend.text;
  element.classList.remove(...Object.values(TONES));
  element.classList.add(TONES[trend.tone]);
}

function renderTiles({ activeOrders, completed, needAttention }) {
  document.getElementById('active-count').textContent = activeOrders.count;
  setTrend(document.getElementById('active-trend'), format.weekTrend(activeOrders.changeThisWeek));
  document.getElementById('completed-count').textContent = completed.last30Days;
  setTrend(
    document.getElementById('completed-trend'),
    format.completedTrend(completed.last30Days, completed.previous30Days));

  document.getElementById('attention-total').textContent = needAttention.total;
  const awaiting = document.getElementById('attention-awaiting');
  awaiting.textContent = format.awaitingActionText(needAttention.awaitingAction);
  awaiting.hidden = needAttention.awaitingAction === 0;
  const alerts = document.getElementById('attention-alerts');
  alerts.textContent = format.alertsText(needAttention.alerts, needAttention.alertOrders);
  alerts.hidden = needAttention.alerts === 0;
}

function renderActiveOrders(orders) {
  const grid = document.getElementById('active-orders');
  const cards = orders.map(order => renderCard(order, { openAlertRefs: true }));
  grid.replaceChildren(...(cards.length === 0 ? [stateBox(format.NO_ACTIVE_ORDERS)] : cards));
}

function renderActivity({ period: bucketPeriod, buckets, summary }) {
  document.getElementById('completed-figure').textContent = summary.completedLast30Days;
  document.getElementById('spend-figure').textContent = format.formatMoney(summary.spendLast30Days);

  const labels = buckets.map(bucket => format.bucketLabel(bucket.start, bucketPeriod));
  drawBars(plotOf('completed-chart'), buckets.map(bucket => bucket.completedOrders), labels);
  drawLine(plotOf('spend-chart'), buckets.map(bucket => bucket.spend), labels);
  renderSummary(summary, bucketPeriod);
}

function plotOf(chartId) {
  return document.querySelector(`#${chartId} [data-plot]`);
}

function svgElement(name, attributes, text) {
  const element = document.createElementNS(SVG, name);
  for (const [attribute, value] of Object.entries(attributes)) {
    element.setAttribute(attribute, value);
  }

  if (text !== undefined) {
    element.textContent = text;
  }

  return element;
}

function axisLabels(labels, xs, anchorOf) {
  return format.labelIndexes(labels.length).map(index =>
    svgElement('text', { ...AXIS_LABEL, x: xs[index], 'text-anchor': anchorOf(index) }, labels[index]));
}

function drawBars(plot, values, labels) {
  if (format.isFlat(values)) {
    plot.replaceChildren(svgElement('text', NO_DATA, format.CHART_NO_DATA));
    return;
  }

  const { bars, labelXs } = format.barChart(values);
  plot.replaceChildren(
    ...bars.map(bar => svgElement('rect', { ...BAR, ...bar })),
    ...axisLabels(labels, labelXs, () => 'middle'));
}

// A centred label on the first point would be clipped at the left edge.
function drawLine(plot, values, labels) {
  if (format.isFlat(values)) {
    plot.replaceChildren(svgElement('text', NO_DATA, format.CHART_NO_DATA));
    return;
  }

  const { points, line, area, peakIndex } = format.lineChart(values);
  plot.replaceChildren(
    svgElement('path', { ...AREA, d: area }),
    svgElement('path', { ...LINE, d: line }),
    ...points.map(({ x, y }, index) =>
      svgElement('circle', { ...(index === peakIndex ? PEAK_DOT : DOT), cx: x, cy: y })),
    ...axisLabels(labels, points.map(point => point.x), index => (index === 0 ? 'start' : 'middle')));
}

function emphasis(text, className) {
  const strong = document.createElement('strong');
  strong.textContent = text;
  if (className) {
    strong.className = className;
  }

  return strong;
}

// A summary line: its lead, its figure in bold when it has one, and the rest.
function fillSummaryLine(element, line, className) {
  const figure = line.emphasis === null ? [] : [emphasis(line.emphasis, className)];
  element.replaceChildren(line.lead, ...figure, line.text);
}

function renderSummary(summary, bucketPeriod) {
  const change = format.completedChange(summary);
  fillSummaryLine(document.getElementById('summary-completed'), change, TONES[change.tone]);
  fillSummaryLine(
    document.getElementById('summary-spend'),
    format.spendSummary(summary.spendLast30Days, summary.averageSpendPerOrder),
    'spend-emphasis');

  const best = document.getElementById('summary-best');
  best.hidden = summary.bestBucket === null;
  if (summary.bestBucket) {
    best.textContent = format.bestBucketText(summary.bestBucket, bucketPeriod);
  }
}
