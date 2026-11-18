namespace Freitty.Application.Dashboard;

// The tiles and the chart summary, read by one statement so that the two cannot disagree. Completed counts the
// rolling window before now and the one before it. The percent is null when the previous window is empty, the
// average when nothing was invoiced in the last window.
public sealed record DashboardFigures(
    int ActiveOrders,
    int ActiveChangeThisWeek,
    int CompletedLast30Days,
    int CompletedPrevious30Days,
    int? CompletedChangePercent,
    decimal SpendLast30Days,
    decimal? AverageSpendPerOrder);
