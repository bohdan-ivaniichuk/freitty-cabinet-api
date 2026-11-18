using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.CountOrders;

// Each counter is the total the list would return for its tab under the same filters. One statement groups the
// orders by type and status — eight rows at most — and the tabs are folded from it; the Drafts tab ignores the
// status filter, since it is a status itself. Alerts cannot be folded from that grouping and is counted apart.
public sealed class CountOrdersQueryHandler(IAppDbContext db, ICurrentUser currentUser, OrderFilter filter)
    : ICommandHandler<CountOrdersQuery, OrderCountsDto>
{
    public async Task<OrderCountsDto> ExecuteAsync(CountOrdersQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);

        List<GroupCount> groups = await Filtered(query, user, status: null)
            .GroupBy(order => new { order.Type, order.Status })
            .Select(group => new GroupCount(group.Key.Type, group.Key.Status, group.Count()))
            .ToListAsync(ct);
        int alerts = await OrderFilter.WithOpenAlert(Filtered(query, user, query.Status)).CountAsync(ct);

        List<GroupCount> inRequestedStatus = groups
            .Where(group => query.Status is null || group.Status == query.Status)
            .ToList();
        return new OrderCountsDto(
            inRequestedStatus.Sum(group => group.Count),
            inRequestedStatus.Where(group => group.Type == OrderType.CrossDock).Sum(group => group.Count),
            inRequestedStatus.Where(group => group.Type == OrderType.Consolidation).Sum(group => group.Count),
            alerts,
            groups.Where(group => group.Status == OrderStatus.Draft).Sum(group => group.Count));
    }

    private IQueryable<Order> Filtered(CountOrdersQuery query, CurrentUser user, OrderStatus? status)
    {
        return filter.Apply(
            db.Orders.AsNoTracking(),
            user.CompanyId,
            query.HubId,
            query.DatePreset,
            status,
            query.Search);
    }

    private sealed record GroupCount(OrderType Type, OrderStatus Status, int Count);
}
