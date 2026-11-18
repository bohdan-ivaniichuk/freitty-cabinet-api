namespace Freitty.Application.Orders.Cards;

// What OrderProgress needs to know about one shipment.
public sealed record ShipmentProgress(bool HasArrived, bool HasOperation, bool IsReceived, bool HasStock);
