using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Domain.Common;

namespace Freitty.Application.Orders.GetOrderDetail;

public sealed class GetOrderDetailQueryHandler(OrderDetailReader detailReader, ICurrentUser currentUser)
    : ICommandHandler<GetOrderDetailQuery, Result<OrderDetailDto>>
{
    public async Task<Result<OrderDetailDto>> ExecuteAsync(GetOrderDetailQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        OrderDetailDto? detail = await detailReader.ReadAsync(user.CompanyId, query.OrderNumber, ct);
        if (detail is null)
        {
            return OrderRequestErrors.OrderNotFound(query.OrderNumber);
        }

        return detail;
    }
}
