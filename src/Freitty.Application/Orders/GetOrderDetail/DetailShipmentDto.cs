namespace Freitty.Application.Orders.GetOrderDetail;

// Arrival and Dock are null until the hub records them.
public sealed record DetailShipmentDto(
    int Position,
    string Label,
    string RefNumber,
    DateTimeOffset? ReceivedAt,
    string? WarehouseNote,
    ArrivalDto? Arrival,
    DockDto? Dock,
    IReadOnlyList<ShipmentQuantityDto> Quantities,
    IReadOnlyList<AlertDto> Alerts,
    IReadOnlyList<OperationDto> Operations);
