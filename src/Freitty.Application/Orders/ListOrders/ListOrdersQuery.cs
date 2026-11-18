using FastEndpoints;
using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.ListOrders;

// The tabs are filters too: Type for Cross-Dock and Consolidation, HasOpenAlerts for Alerts, and Status = Draft for
// Drafts. Every parameter has a default, which also tells the OpenAPI document that it may be left out.
public sealed record ListOrdersQuery(
    OrderType? Type = null,
    OrderStatus? Status = null,
    bool HasOpenAlerts = false,
    Guid? HubId = null,
    DatePreset? DatePreset = null,
    string? Search = null,
    int Page = ListOrdersQuery.FirstPage,
    int PageSize = ListOrdersQuery.DefaultPageSize)
    : ICommand<OrderPageDto>
{
    public const int FirstPage = 1;
    public const int DefaultPageSize = 12;
}
