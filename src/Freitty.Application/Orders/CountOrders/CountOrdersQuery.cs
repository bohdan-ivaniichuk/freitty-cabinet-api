using FastEndpoints;
using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.CountOrders;

public sealed record CountOrdersQuery(
    Guid? Hub = null,
    DatePreset? Date = null,
    OrderStatus? Status = null,
    string? Q = null)
    : ICommand<OrderCountsDto>;
