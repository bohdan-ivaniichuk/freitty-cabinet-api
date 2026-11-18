using Freitty.Domain.Orders;

namespace Freitty.Application.Dashboard;

// An Active order is one that is Ready or In progress: the tile counts them and the cards show the first of them.
public static class ActiveOrderStatuses
{
    public static readonly IReadOnlyList<OrderStatus> All = [OrderStatus.Ready, OrderStatus.InProgress];
}
