using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.ExportOrders;

// The whole filtered list in the list's order, one statement: the sums and counts are subqueries of the projection.
public sealed class ExportOrdersQueryHandler(IAppDbContext db, ICurrentUser currentUser, OrderFilter filter)
    : ICommandHandler<ExportOrdersQuery, IReadOnlyList<OrderExportRow>>
{
    // Bounds the size of one response; far above what one company's list holds today.
    public const int MaxExportRows = 10_000;

    public async Task<IReadOnlyList<OrderExportRow>> ExecuteAsync(ExportOrdersQuery query, CancellationToken ct)
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

        return await OrderListSort.Apply(orders)
            .Take(MaxExportRows)
            .Select(order => new OrderExportRow(
                order.Number,
                order.Type,
                order.Status,
                order.Hub.Name,
                order.ScheduledAt,
                order.DestinationCity,
                order.DestinationRegion,
                order.TransportKind,
                order.Carrier == null ? null : order.Carrier.Name,
                order.DriverName,
                order.Shipments.Count(),
                order.Shipments.SelectMany(shipment => shipment.CargoLines).Sum(line => line.Quantity),
                order.Shipments
                    .SelectMany(shipment => shipment.Operations)
                    .Where(operation => operation.Type == OperationType.Unloading)
                    .Sum(operation => operation.Quantity),
                order.Shipments
                    .SelectMany(shipment => shipment.Alerts)
                    .Count(alert => alert.ResolvedAt == null),
                order.CreatedBy.Name))
            .ToListAsync(ct);
    }
}
