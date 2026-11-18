using FastEndpoints;
using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.ExportOrders;

// The filters and tabs of the list, with the same meaning; the export has no pages.
public sealed record ExportOrdersQuery(
    OrderType? Type = null,
    OrderStatus? Status = null,
    bool HasOpenAlerts = false,
    Guid? HubId = null,
    DatePreset? DatePreset = null,
    string? Search = null)
    : ICommand<IReadOnlyList<OrderExportRow>>;
