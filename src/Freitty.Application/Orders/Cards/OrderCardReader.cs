using Freitty.Application.Abstractions;
using Freitty.Application.Common;
using Freitty.Domain.Billing;
using Freitty.Domain.Companies;
using Freitty.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Application.Orders.Cards;

// Reads the cards of orders already chosen, filtered and paged by the caller, with four statements whatever the
// number of orders: the orders, their shipments, the ledger sums and the open alerts. No entity is loaded. Every
// statement is held to the company as well, so a caller's id of another company's order yields no card.
public sealed class OrderCardReader(IAppDbContext db)
{
    public async Task<IReadOnlyList<OrderCardDto>> ReadAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return [];
        }

        Dictionary<Guid, OrderRow> orders = await ReadOrders(companyId, orderIds)
            .ToDictionaryAsync(order => order.Id, cancellationToken);
        List<ShipmentRow> shipments = await ReadShipments(companyId, orderIds).ToListAsync(cancellationToken);
        List<UnitTypeSums> sums = await UnitTypeSums.Of(ShipmentsOf(companyId, orderIds))
            .ToListAsync(cancellationToken);
        List<OpenAlertRow> openAlerts = await ReadOpenAlerts(companyId, orderIds).ToListAsync(cancellationToken);

        var lookups = new ShipmentLookups(
            shipments.ToLookup(shipment => shipment.OrderId),
            ShipmentQuantities.ByShipment(sums, ReceivedShipmentIds(shipments)),
            openAlerts.ToLookup(alert => alert.ShipmentId, alert => alert.Type));
        return orderIds
            .Where(orders.ContainsKey)
            .Select(orderId => ToCard(orders[orderId], lookups))
            .ToList();
    }

    private static HashSet<Guid> ReceivedShipmentIds(List<ShipmentRow> shipments)
    {
        return shipments.Where(shipment => shipment.IsReceived).Select(shipment => shipment.Id).ToHashSet();
    }

    private static OrderCardDto ToCard(OrderRow order, ShipmentLookups lookups)
    {
        List<ShipmentRow> shipments = lookups.Shipments[order.Id].ToList();
        List<ShipmentQuantities> quantities = shipments
            .SelectMany(shipment => lookups.Quantities[shipment.Id])
            .ToList();
        List<AlertType> openAlertTypes = shipments
            .SelectMany(shipment => lookups.OpenAlertTypes[shipment.Id])
            .Distinct()
            .Order()
            .ToList();

        OrderStage stage = OrderProgress.StageOf(
            order.Status,
            shipments.Select(shipment => ProgressOf(shipment, lookups)).ToList());

        return new OrderCardDto(
            order.Number,
            order.Type,
            order.CrossDockKind,
            order.Status,
            openAlertTypes,
            UserDto.Of(order.CreatedByName, order.CreatedByRole),
            order.Hub,
            order.TransferHub,
            order.ScheduledAt,
            order.Destination,
            order.Transport,
            DeclaredPerUnitType(quantities),
            quantities.Sum(quantity => quantity.Declared),
            quantities.Sum(quantity => quantity.Actual),
            quantities.Exists(quantity => quantity.HasQuantityDelta),
            order.OutboundTrailers,
            shipments.Select(shipment => ToCardShipment(order.Number, shipment, lookups)).ToList(),
            OrderProgress.NextStepOf(stage, openAlertTypes, order.FirstUnpaidInvoice, order.LastInvoice));
    }

    // A Unit Type that was unloaded but never declared has quantities with nothing declared; it is no declared line.
    private static List<UnitQuantityDto> DeclaredPerUnitType(List<ShipmentQuantities> quantities)
    {
        return quantities
            .GroupBy(quantity => quantity.UnitType)
            .Select(group => new UnitQuantityDto(group.Key, group.Sum(quantity => quantity.Declared)))
            .Where(declared => declared.Quantity > 0)
            .OrderBy(declared => declared.UnitType)
            .ToList();
    }

    private static CardShipmentDto ToCardShipment(string orderNumber, ShipmentRow shipment, ShipmentLookups lookups)
    {
        IEnumerable<ShipmentQuantities> quantities = lookups.Quantities[shipment.Id];
        return new CardShipmentDto(
            ShipmentLabel.Of(orderNumber, shipment.Position),
            shipment.RefNumber,
            quantities.Sum(quantity => quantity.Declared),
            quantities.Sum(quantity => quantity.Actual),
            lookups.OpenAlertTypes[shipment.Id].Order().ToList());
    }

    private static ShipmentProgress ProgressOf(ShipmentRow shipment, ShipmentLookups lookups)
    {
        return ShipmentProgress.Of(
            hasArrived: shipment.HasArrived,
            hasOperation: shipment.HasOperation,
            isReceived: shipment.IsReceived,
            lookups.Quantities[shipment.Id]);
    }

    private IQueryable<Order> OrdersOf(Guid companyId, IReadOnlyCollection<Guid> orderIds)
    {
        return db.Orders
            .AsNoTracking()
            .Where(order => order.CompanyId == companyId && orderIds.Contains(order.Id));
    }

    private IQueryable<Shipment> ShipmentsOf(Guid companyId, IReadOnlyCollection<Guid> orderIds)
    {
        return OrdersOf(companyId, orderIds).SelectMany(order => order.Shipments);
    }

    // EF Core turns each invoice lookup into a window over the invoices it is given; restating the page's ids keeps
    // that window to the invoices of the page instead of the whole table.
    private IQueryable<OrderRow> ReadOrders(Guid companyId, IReadOnlyCollection<Guid> orderIds)
    {
        IQueryable<Invoice> invoicesOfPage = db.Invoices.Where(invoice => orderIds.Contains(invoice.OrderId));
        return OrdersOf(companyId, orderIds)
            .Select(order => new OrderRow(
                order.Id,
                order.Number,
                order.Type,
                order.CrossDockKind,
                order.Status,
                order.CreatedBy.Name,
                order.CreatedBy.Role,
                new CardHubDto(order.Hub.Name, order.Hub.ShortName, order.Hub.TimeZone),
                order.TransferHub == null
                    ? null
                    : new TransferHubDto(order.TransferHub.Name, order.TransferHub.ShortName),
                order.ScheduledAt,
                order.DestinationCity == null
                    ? null
                    : new DestinationDto(order.DestinationCity, order.DestinationRegion!),
                order.TransportKind == null
                    ? null
                    : new CardTransportDto(
                        order.TransportKind.Value,
                        order.Carrier == null ? null : order.Carrier.Name,
                        order.DriverName),
                order.Shipments
                    .SelectMany(shipment => shipment.Operations)
                    .Where(operation => operation.Type == OperationType.Loading)
                    .Select(operation => operation.TrailerNumber)
                    .Distinct()
                    .Count(),
                invoicesOfPage
                    .Where(invoice => invoice.OrderId == order.Id && invoice.PaidAt == null)
                    .OrderBy(invoice => invoice.IssuedAt)
                    .ThenBy(invoice => invoice.Id)
                    .Select(invoice => new InvoiceSummary(invoice.Number, invoice.Amount))
                    .FirstOrDefault(),
                invoicesOfPage
                    .Where(invoice => invoice.OrderId == order.Id)
                    .OrderByDescending(invoice => invoice.IssuedAt)
                    .ThenByDescending(invoice => invoice.Id)
                    .Select(invoice => new InvoiceSummary(invoice.Number, invoice.Amount))
                    .FirstOrDefault()));
    }

    private IQueryable<ShipmentRow> ReadShipments(Guid companyId, IReadOnlyCollection<Guid> orderIds)
    {
        return ShipmentsOf(companyId, orderIds)
            .OrderBy(shipment => shipment.OrderId)
            .ThenBy(shipment => shipment.Position)
            .Select(shipment => new ShipmentRow(
                shipment.Id,
                shipment.OrderId,
                shipment.Position,
                shipment.RefNumber,
                shipment.ArrivedAt != null,
                shipment.ReceivedAt != null,
                shipment.Operations.Any()));
    }

    private IQueryable<OpenAlertRow> ReadOpenAlerts(Guid companyId, IReadOnlyCollection<Guid> orderIds)
    {
        return ShipmentsOf(companyId, orderIds)
            .SelectMany(shipment => shipment.Alerts)
            .Where(alert => alert.ResolvedAt == null)
            .Select(alert => new OpenAlertRow(alert.ShipmentId, alert.Type));
    }

    private sealed record OrderRow(
        Guid Id,
        string Number,
        OrderType Type,
        CrossDockKind? CrossDockKind,
        OrderStatus Status,
        string CreatedByName,
        UserRole CreatedByRole,
        CardHubDto Hub,
        TransferHubDto? TransferHub,
        DateTimeOffset? ScheduledAt,
        DestinationDto? Destination,
        CardTransportDto? Transport,
        int OutboundTrailers,
        InvoiceSummary? FirstUnpaidInvoice,
        InvoiceSummary? LastInvoice);

    private sealed record ShipmentRow(
        Guid Id,
        Guid OrderId,
        int Position,
        string RefNumber,
        bool HasArrived,
        bool IsReceived,
        bool HasOperation);

    private sealed record OpenAlertRow(Guid ShipmentId, AlertType Type);

    // The shipment rows of the page by order, and their quantities and open alert types by shipment.
    private sealed record ShipmentLookups(
        ILookup<Guid, ShipmentRow> Shipments,
        ILookup<Guid, ShipmentQuantities> Quantities,
        ILookup<Guid, AlertType> OpenAlertTypes);
}
