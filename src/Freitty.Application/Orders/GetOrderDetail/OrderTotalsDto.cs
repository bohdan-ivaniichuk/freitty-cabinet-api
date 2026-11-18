
namespace Freitty.Application.Orders.GetOrderDetail;

// Declared, Actual and OnStock have one entry per Unit Type of any shipment's quantities, zeros included.
// QuantityDeltas counts the (shipment, Unit Type) pairs with a known, non-zero delta.
public sealed record OrderTotalsDto(
    IReadOnlyList<UnitQuantityDto> Declared,
    IReadOnlyList<UnitQuantityDto> Actual,
    IReadOnlyList<UnitQuantityDto> OnStock,
    int QuantityDeltas,
    int Photos,
    int Comments,
    int OutboundTrailers);
