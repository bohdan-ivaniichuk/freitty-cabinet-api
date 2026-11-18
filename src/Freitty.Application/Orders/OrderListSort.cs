using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

// The order of the Order List, which the dashboard's cards follow too: newest Scheduled date first, with unscheduled
// drafts on top — PostgreSQL puts NULL first when descending. The id comes last, so pages never repeat or skip an
// order.
internal static class OrderListSort
{
    public static IQueryable<Order> Apply(IQueryable<Order> orders)
    {
        return orders
            .OrderByDescending(order => order.ScheduledAt)
            .ThenByDescending(order => order.Id);
    }
}
