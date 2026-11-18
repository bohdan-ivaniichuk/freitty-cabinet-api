using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Application.Orders;
using Freitty.Application.Orders.Cards;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Dashboard.GetDashboard;

// The aggregates come from the dashboard reader, the cards from the card reader of the list: one definition of a
// card. The best bucket is picked from the series the chart draws, so it is always one of its bars.
public sealed class GetDashboardQueryHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    IDashboardReader dashboardReader,
    OrderCardReader cardReader,
    TimeProvider clock)
    : ICommandHandler<GetDashboardQuery, DashboardDto>
{
    private const int CardCount = 4;
    private const int AlertOrderCount = 3;

    // "Completed (30 d)" is rolling: the last 30 times 24 hours, compared with the 30 before them.
    private static readonly TimeSpan RollingWindow = TimeSpan.FromDays(30);

    public async Task<DashboardDto> ExecuteAsync(GetDashboardQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        DateTimeOffset now = clock.GetUtcNow();

        DashboardFigures figures = await dashboardReader.ReadFiguresAsync(user.CompanyId, now, RollingWindow, ct);
        IReadOnlyList<ActivityBucketDto> buckets =
            await dashboardReader.ReadActivityAsync(user.CompanyId, now, ActivityAxis.Of(query.Period), ct);
        AttentionCounts attention = await dashboardReader.ReadAttentionCountsAsync(user.CompanyId, ct);
        IReadOnlyList<AlertOrderDto> alertOrders =
            await dashboardReader.ReadAlertOrdersAsync(user.CompanyId, AlertOrderCount, ct);
        IReadOnlyList<OrderCardDto> cards = await ReadActiveCardsAsync(user.CompanyId, ct);

        var summary = new ActivitySummaryDto(
            figures.CompletedLast30Days,
            figures.CompletedChangePercent,
            figures.SpendLast30Days,
            figures.AverageSpendPerOrder,
            BestOf(buckets));
        return new DashboardDto(
            new ActiveOrdersDto(figures.ActiveOrders, figures.ActiveChangeThisWeek),
            new CompletedOrdersDto(figures.CompletedLast30Days, figures.CompletedPrevious30Days),
            new NeedAttentionDto(attention.Total, attention.AwaitingAction, attention.Alerts, alertOrders),
            cards,
            new ActivityDto(query.Period, buckets, summary));
    }

    // The highest spend, the later bucket on a tie; none when nothing was spent.
    private static ActivityBucketDto? BestOf(IReadOnlyList<ActivityBucketDto> buckets)
    {
        return buckets
            .Where(bucket => bucket.Spend > 0)
            .OrderByDescending(bucket => bucket.Spend)
            .ThenByDescending(bucket => bucket.Start)
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<OrderCardDto>> ReadActiveCardsAsync(Guid companyId, CancellationToken ct)
    {
        IQueryable<Order> activeOrders = db.Orders
            .AsNoTracking()
            .Where(order => order.CompanyId == companyId && ActiveOrderStatuses.All.Contains(order.Status));
        List<Guid> orderIds = await OrderListSort.Apply(activeOrders)
            .Take(CardCount)
            .Select(order => order.Id)
            .ToListAsync(ct);
        return await cardReader.ReadAsync(companyId, orderIds, ct);
    }
}
