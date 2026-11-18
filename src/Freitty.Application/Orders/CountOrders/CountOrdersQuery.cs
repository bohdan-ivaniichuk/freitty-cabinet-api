using FastEndpoints;
using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.CountOrders;

public sealed record CountOrdersQuery(
    Guid? HubId = null,
    DatePreset? DatePreset = null,
    OrderStatus? Status = null,
    string? Search = null)
    : ICommand<OrderCountsDto>;
