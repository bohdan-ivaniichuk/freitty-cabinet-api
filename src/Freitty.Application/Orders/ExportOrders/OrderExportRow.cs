using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.ExportOrders;

// One order of the export. CarrierName is set for a Carrier, DriverName for an own driver; self pickup has neither.
// The quantities are sums over all shipments and Unit Types; ActualQuantity is what was unloaded.
public sealed record OrderExportRow(
    string Number,
    OrderType Type,
    OrderStatus Status,
    string HubName,
    DateTimeOffset? ScheduledAt,
    string? DestinationCity,
    string? DestinationRegion,
    TransportKind? TransportKind,
    string? CarrierName,
    string? DriverName,
    int ShipmentCount,
    int DeclaredQuantity,
    int ActualQuantity,
    int OpenAlertCount,
    string CreatedByName);
