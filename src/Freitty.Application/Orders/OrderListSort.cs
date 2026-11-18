using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

// The order of the Order List, which the dashboard's cards and the pipeline's columns follow too: newest Scheduled
// date first, with unscheduled drafts on top — PostgreSQL puts NULL first when descending. The id comes last, so
// pages never repeat or skip an order.
internal static class OrderListSort
{
    public static IQueryable<Order> Apply(IQueryable<Order> orders)
    {
        return orders
            .OrderByDescending(order => order.ScheduledAt)
            .ThenByDescending(order => order.Id);
    }

    // The first orders of each status in the same order. EF Core turns the correlated Take into ROW_NUMBER() over a
    // partition per status, joined to the statuses present; a GroupBy followed by Take is not translated. The keys
    // are written out again because the inner query is part of the expression, where Apply cannot be called. The rows
    // come back in no particular order.
    public static IQueryable<Order> FirstOfEachStatus(IQueryable<Order> orders, int countPerStatus)
    {
        return orders
            .Select(order => order.Status)
            .Distinct()
            .SelectMany(status => orders
                .Where(order => order.Status == status)
                .OrderByDescending(order => order.ScheduledAt)
                .ThenByDescending(order => order.Id)
                .Take(countPerStatus));
    }
}
