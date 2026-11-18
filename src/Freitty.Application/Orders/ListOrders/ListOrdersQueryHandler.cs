using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Application.Orders.Cards;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.ListOrders;

// Two steps: the page of ids with its total, then the cards of that page only.
public sealed class ListOrdersQueryHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    OrderFilter filter,
    OrderCardReader cardReader)
    : ICommandHandler<ListOrdersQuery, OrderPageDto>
{
    public async Task<OrderPageDto> ExecuteAsync(ListOrdersQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        IQueryable<Order> orders = filter.Apply(
            db.Orders.AsNoTracking(),
            user.CompanyId,
            query.Hub,
            query.Date,
            query.Status,
            query.Q);

        if (query.Type is { } type)
        {
            orders = orders.Where(order => order.Type == type);
        }

        if (query.Alerts)
        {
            orders = OrderFilter.WithOpenAlert(orders);
        }

        int total = await orders.CountAsync(ct);
        List<Guid> pageIds = await OrderListSort.Apply(orders)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(order => order.Id)
            .ToListAsync(ct);

        IReadOnlyList<OrderCardDto> cards = await cardReader.ReadAsync(user.CompanyId, pageIds, ct);
        return new OrderPageDto(cards, query.Page, query.PageSize, total);
    }
}
