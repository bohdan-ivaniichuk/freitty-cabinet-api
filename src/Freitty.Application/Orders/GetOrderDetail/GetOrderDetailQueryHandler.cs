using FastEndpoints;
using Freitty.Application.Abstractions;
using Freitty.Application.Common;
using Freitty.Domain.Common;
using Freitty.Domain.Companies;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.GetOrderDetail;

// A fixed number of statements, none of which loads an entity: the order with its references and collections as
// split queries — so no statement returns the product of two collections — then its invoices and the ledger sums
// of its shipments. The quantities come from the same arithmetic as the cards, and the stage from OrderProgress.
// The statements do not share a snapshot, so a write committed between them can show in part. That is acceptable
// for a page: the version is read in the first statement, and a write decided on a stale page is refused with 409.
public sealed class GetOrderDetailQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<GetOrderDetailQuery, Result<OrderDetailDto>>
{
    // EF Core casts the quantity to the money type, so quantity times price has four decimals in PostgreSQL; money
    // leaves with two.
    private const int MoneyDecimals = 2;

    public async Task<Result<OrderDetailDto>> ExecuteAsync(GetOrderDetailQuery query, CancellationToken ct)
    {
        CurrentUser user = await currentUser.GetAsync(ct);
        OrderRow? order = await ReadOrder(user.CompanyId, query.Number).FirstOrDefaultAsync(ct);
        if (order is null)
        {
            return ApplicationErrors.OrderNotFound(query.Number);
        }

        List<InvoiceDto> invoices = await ReadInvoices(order.Id).ToListAsync(ct);
        List<UnitTypeSums> sums = await UnitTypeSums.Of(ShipmentsOf(order.Id)).ToListAsync(ct);

        HashSet<Guid> receivedShipmentIds = order.Shipments
            .Where(shipment => shipment.ReceivedAt is not null)
            .Select(shipment => shipment.Id)
            .ToHashSet();
        return ToDetail(order, ShipmentQuantities.ByShipment(sums, receivedShipmentIds), invoices);
    }

    private static OrderDetailDto ToDetail(
        OrderRow order,
        ILookup<Guid, ShipmentQuantities> quantities,
        List<InvoiceDto> invoices)
    {
        List<ShipmentQuantities> allQuantities = order.Shipments
            .SelectMany(shipment => quantities[shipment.Id])
            .ToList();
        OrderStage stage = OrderProgress.StageOf(
            order.Status,
            order.Shipments.Select(shipment => ProgressOf(shipment, quantities[shipment.Id])).ToList());

        var totals = new OrderTotalsDto(
            SumPerUnitType(allQuantities, quantity => quantity.Declared),
            SumPerUnitType(allQuantities, quantity => quantity.Actual),
            SumPerUnitType(allQuantities, quantity => quantity.OnStock),
            allQuantities.Count(quantity => quantity.HasQuantityDelta),
            order.Photos,
            order.Comments,
            order.OutboundTrailers);

        return new OrderDetailDto(
            order.Number,
            order.Type,
            order.CrossDockKind,
            order.Status,
            stage,
            order.Version,
            UserDto.Of(order.CreatedByName, order.CreatedByRole),
            order.Company,
            order.Hub,
            order.TransferHub,
            order.ScheduledAt,
            order.Destination,
            order.Transport,
            order.Services,
            totals,
            order.Shipments.Select(shipment => ToShipment(order.Number, shipment, quantities[shipment.Id])).ToList(),
            new SuppliesDto(order.SupplyLines, order.SupplySubtotal),
            invoices);
    }

    private static ShipmentProgress ProgressOf(ShipmentRow shipment, IEnumerable<ShipmentQuantities> quantities)
    {
        return ShipmentProgress.Of(
            hasArrived: shipment.Arrival is not null,
            hasOperation: shipment.Operations.Count > 0,
            isReceived: shipment.ReceivedAt is not null,
            quantities);
    }

    private static List<UnitQuantityDto> SumPerUnitType(
        List<ShipmentQuantities> quantities,
        Func<ShipmentQuantities, int> quantityOf)
    {
        return quantities
            .GroupBy(quantity => quantity.UnitType)
            .OrderBy(group => group.Key)
            .Select(group => new UnitQuantityDto(group.Key, group.Sum(quantityOf)))
            .ToList();
    }

    private static DetailShipmentDto ToShipment(
        string orderNumber,
        ShipmentRow shipment,
        IEnumerable<ShipmentQuantities> quantities)
    {
        return new DetailShipmentDto(
            shipment.Position,
            ShipmentLabel.Of(orderNumber, shipment.Position),
            shipment.RefNumber,
            shipment.ReceivedAt,
            shipment.WarehouseNote,
            shipment.Arrival,
            shipment.Dock,
            quantities
                .Select(quantity => new ShipmentQuantityDto(
                    quantity.UnitType,
                    quantity.Declared,
                    quantity.Actual,
                    quantity.KnownDelta,
                    quantity.OnStock))
                .ToList(),
            shipment.Alerts,
            shipment.Operations);
    }

    // The whole number, compared in lower case: a pattern match would make a "_" in the route a wildcard.
    private IQueryable<OrderRow> ReadOrder(Guid companyId, string number)
    {
        string lowerCaseNumber = number.ToLowerInvariant();
        return db.Orders
            .AsNoTracking()
            .Where(order => order.CompanyId == companyId && order.Number.ToLower() == lowerCaseNumber)
            .AsSplitQuery()
            .Select(order => new OrderRow(
                order.Id,
                order.Number,
                order.Type,
                order.CrossDockKind,
                order.Status,
                order.Version,
                order.CreatedBy.Name,
                order.CreatedBy.Role,
                new DetailCompanyDto(order.Company.Name),
                new DetailHubDto(order.Hub.Name, order.Hub.ShortName, order.Hub.Region, order.Hub.TimeZone),
                order.TransferHub == null
                    ? null
                    : new TransferHubDto(order.TransferHub.Name, order.TransferHub.ShortName),
                order.ScheduledAt,
                order.DestinationCity == null
                    ? null
                    : new DestinationDto(order.DestinationCity, order.DestinationRegion!),
                order.TransportKind == null
                    ? null
                    : new DetailTransportDto(
                        order.TransportKind.Value,
                        order.Carrier == null ? null : order.Carrier.Name,
                        order.Carrier == null ? null : order.Carrier.Phone,
                        order.DriverName),
                order.Services
                    .Select(service => service.Service.Name)
                    .OrderBy(name => name)
                    .ToList(),
                order.Shipments
                    .SelectMany(shipment => shipment.Operations)
                    .Sum(operation => operation.PhotoCount),
                order.Shipments
                    .SelectMany(shipment => shipment.Operations)
                    .Sum(operation => operation.CommentCount),
                order.Shipments
                    .SelectMany(shipment => shipment.Operations)
                    .Where(operation => operation.Type == OperationType.Loading)
                    .Select(operation => operation.TrailerNumber)
                    .Distinct()
                    .Count(),
                order.SupplyLines
                    .OrderBy(line => line.SupplyItem.Category)
                    .ThenBy(line => line.SupplyItem.Sku)
                    .ThenBy(line => line.Id)
                    .Select(line => new SupplyLineDto(
                        line.SupplyItem.Sku,
                        line.SupplyItem.Category,
                        line.Quantity,
                        line.UnitPrice,
                        Math.Round(line.Quantity * line.UnitPrice, MoneyDecimals)))
                    .ToList(),
                Math.Round(order.SupplyLines.Sum(line => line.Quantity * line.UnitPrice), MoneyDecimals),
                order.Shipments
                    .OrderBy(shipment => shipment.Position)
                    .Select(shipment => new ShipmentRow(
                        shipment.Id,
                        shipment.Position,
                        shipment.RefNumber,
                        shipment.ReceivedAt,
                        shipment.WarehouseNote,
                        shipment.ArrivedAt == null
                            ? null
                            : new ArrivalDto(
                                shipment.TrailerType!.Value,
                                shipment.TruckNumber!,
                                shipment.TrailerNumber!,
                                shipment.ArrivedAt.Value),
                        shipment.DockAssignedAt == null
                            ? null
                            : new DockDto(
                                shipment.Dock!,
                                shipment.Bay!,
                                shipment.FloorLead!,
                                shipment.DockAssignedAt.Value),
                        shipment.Alerts
                            .OrderBy(alert => alert.RaisedAt)
                            .ThenBy(alert => alert.Type)
                            .ThenBy(alert => alert.Id)
                            .Select(alert => new AlertDto(alert.Id, alert.Type, alert.RaisedAt, alert.ResolvedAt))
                            .ToList(),
                        shipment.Operations
                            .OrderBy(operation => operation.AppliedAt)
                            .ThenBy(operation => operation.Id)
                            .Select(operation => new OperationDto(
                                operation.Id,
                                operation.Type,
                                operation.UnitType,
                                operation.Quantity,
                                operation.TrailerNumber,
                                operation.AppliedAt,
                                operation.IsBillable,
                                operation.PhotoCount,
                                operation.CommentCount))
                            .ToList()))
                    .ToList()));
    }

    private IQueryable<InvoiceDto> ReadInvoices(Guid orderId)
    {
        return db.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.OrderId == orderId)
            .OrderBy(invoice => invoice.IssuedAt)
            .ThenBy(invoice => invoice.Number)
            .Select(invoice => new InvoiceDto(invoice.Number, invoice.Amount, invoice.IssuedAt, invoice.PaidAt));
    }

    private IQueryable<Shipment> ShipmentsOf(Guid orderId)
    {
        return db.Orders
            .AsNoTracking()
            .Where(order => order.Id == orderId)
            .SelectMany(order => order.Shipments);
    }

    private sealed record OrderRow(
        Guid Id,
        string Number,
        OrderType Type,
        CrossDockKind? CrossDockKind,
        OrderStatus Status,
        int Version,
        string CreatedByName,
        UserRole CreatedByRole,
        DetailCompanyDto Company,
        DetailHubDto Hub,
        TransferHubDto? TransferHub,
        DateTimeOffset? ScheduledAt,
        DestinationDto? Destination,
        DetailTransportDto? Transport,
        List<string> Services,
        int Photos,
        int Comments,
        int OutboundTrailers,
        List<SupplyLineDto> SupplyLines,
        decimal SupplySubtotal,
        List<ShipmentRow> Shipments);

    private sealed record ShipmentRow(
        Guid Id,
        int Position,
        string RefNumber,
        DateTimeOffset? ReceivedAt,
        string? WarehouseNote,
        ArrivalDto? Arrival,
        DockDto? Dock,
        List<AlertDto> Alerts,
        List<OperationDto> Operations);
}
