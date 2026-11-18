using FastEndpoints;
using Freitty.Application.Dashboard;
using Freitty.Application.Dashboard.GetDashboard;

namespace Freitty.Api.Endpoints.Dashboard.GetDashboard;

internal sealed class GetDashboardEndpoint : Endpoint<GetDashboardQuery, DashboardDto>
{
    public override void Configure()
    {
        Get(ApiRoutes.Dashboard.Get);
        Description(endpoint => endpoint.ProducesProblemDetails());
        Summary(summary =>
        {
            summary.Summary = "Get the dashboard";
            summary.Description =
                $"The tiles, the first {GetDashboardQueryHandler.CardCount} active orders and the activity chart " +
                "of the current company. Buckets are cut in the reporting time zone, weeks start on Monday, and " +
                "the series ends with the current bucket; the 30-day windows are rolling.";
            summary.RequestParam(
                query => query.Period,
                $"The unit of a bucket of the chart: {ActivityBuckets.CountOf(ActivityPeriod.Day)} days, " +
                $"{ActivityBuckets.CountOf(ActivityPeriod.Week)} weeks, " +
                $"{ActivityBuckets.CountOf(ActivityPeriod.Month)} months or " +
                $"{ActivityBuckets.CountOf(ActivityPeriod.Quarter)} quarters. Week when left out.");
            summary.Responses[StatusCodes.Status200OK] = "Everything the Dashboard screen shows.";
            summary.Responses[StatusCodes.Status400BadRequest] = "The period is not understood.";
        });
    }

    public override async Task HandleAsync(GetDashboardQuery query, CancellationToken ct)
    {
        DashboardDto dashboard = await query.ExecuteAsync(ct);
        await Send.OkAsync(dashboard, ct);
    }
}
