using FastEndpoints;

namespace Freitty.Application.Dashboard.GetDashboard;

public sealed record GetDashboardQuery(ActivityPeriod Period = ActivityPeriod.Week) : ICommand<DashboardDto>;
