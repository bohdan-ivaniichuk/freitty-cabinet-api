// index.html: GET /api/v1/dashboard for the tiles, the active orders and the activity charts.
// The period is the URL's `period`; the period buttons are links that change it.

import { getJson } from './api.js';
import { openCardsOnClick, renderCard } from './card.js';
import * as format from './format.js';
import { errorBox, initShell, loadMe, stateBox } from './shell.js';

const PERIODS = { day: 'Day', week: 'Week', month: 'Month', quarter: 'Quarter' };
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

// An unknown or empty period would be a 400 for the API: the page asks for the default instead.
const periodKey = new URLSearchParams(location.search).get('period');
const period = PERIODS[Object.hasOwn(PERIODS, periodKey) ? periodKey : DEFAULT_PERIOD];

initShell();
openCardsOnClick(document.getElementById('active-orders'));
markActivePeriod(period);
loadMe().then(renderWelcome);
loadDashboard();

function renderWelcome(me) {
  if (me) {
    document.getElementById('welcome').textContent = `Welcome, ${me.user.name} 👋`;
  }
}

function markActivePeriod(active) {
  for (const link of document.querySelectorAll('[data-period]')) {
    const isActive = PERIODS[link.dataset.period] === active;
    link.classList.toggle('active', isActive);
    if (isActive) {
      link.setAttribute('aria-current', 'page');
    }
  }
}

async function loadDashboard() {
  const state = document.getElementById('dashboard-state');
  try {
    const dashboard = await getJson('/api/v1/dashboard', { period });
    renderTiles(dashboard);
    renderActiveOrders(dashboard.orders);
    renderActivity(dashboard.activity);
    state.remove();
    document.getElementById('dashboard').hidden = false;
  } catch (error) {
    state.replaceWith(errorBox(error));
  }
}

function setTrend(element, trend) {
  element.textContent = trend.text;
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
  grid.replaceChildren(...(cards.length === 0 ? [stateBox('No active orders')] : cards));
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
    plot.replaceChildren(svgElement('text', NO_DATA, 'No data'));
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
    plot.replaceChildren(svgElement('text', NO_DATA, 'No data'));
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

function renderSummary(summary, bucketPeriod) {
  const change = format.completedChange(summary);
  const completed = document.getElementById('summary-completed');
  if (change.emphasis) {
    completed.replaceChildren('📈 ', emphasis(change.emphasis, TONES[change.tone]), change.text);
  } else {
    completed.textContent = `📈 ${change.text}`;
  }

  document.getElementById('summary-spend').replaceChildren(
    '💰 ',
    emphasis(format.formatMoney(summary.spendLast30Days), 'spend-emphasis'),
    format.averageSpendText(summary.averageSpendPerOrder));

  const best = document.getElementById('summary-best');
  best.hidden = summary.best === null;
  if (summary.best) {
    best.textContent = format.bestBucketText(summary.best, bucketPeriod);
  }
}
