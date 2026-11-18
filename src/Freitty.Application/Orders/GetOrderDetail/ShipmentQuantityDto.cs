using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.GetOrderDetail;

// Delta is null while it is not known: a shortage before the shipment is received.
public sealed record ShipmentQuantityDto(UnitType UnitType, int Declared, int Actual, int? Delta, int OnStock);
