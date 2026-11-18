using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Application.Orders.Cards;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.GetPipeline;

// Three steps, each in the database: the totals per status, the ids of the first orders of each status, and the
// cards of those orders only. A status the filters exclude is still a column, empty, so the board keeps its shape.
public sealed class GetPipelineQueryHandler(
    IAppDbContext db,
    ICurrentUser currentUser,
    OrderFilter filter,
    OrderCardReader cardReader)
    : ICommandHandler<GetPipelineQuery, PipelineDto>
{
    public async Task<PipelineDto> ExecuteAsync(GetPipelineQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        IQueryable<Order> orders = filter.ApplyWithTab(
            db.Orders.AsNoTracking(),
            user.CompanyId,
            query.HubId,
            query.DatePreset,
            query.Status,
            query.Search,
            query.Type,
            query.HasOpenAlerts);

        Dictionary<OrderStatus, int> totals = await orders
            .GroupBy(order => order.Status)
            .Select(group => new { Status = group.Key, Total = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Total, ct);
        List<Guid> firstIds = await OrderListSort.Apply(
                OrderListSort.FirstOfEachStatus(orders, GetPipelineQuery.PipelineColumnSize))
            .Select(order => order.Id)
            .ToListAsync(ct);

        IReadOnlyList<OrderCardDto> cards = await cardReader.ReadAsync(user.CompanyId, firstIds, ct);
        return new PipelineDto(Enum.GetValues<OrderStatus>()
            .Select(status => new PipelineColumnDto(
                status,
                totals.GetValueOrDefault(status),
                cards.Where(card => card.Status == status).ToList()))
            .ToList());
    }
}
