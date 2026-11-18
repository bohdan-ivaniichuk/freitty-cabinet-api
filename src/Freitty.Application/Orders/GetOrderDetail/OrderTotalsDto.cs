namespace Freitty.Application.Orders.GetOrderDetail;

// Declared, Actual and OnStock have one entry per Unit Type of any shipment's quantities, zeros included.
// QuantityDeltaCount counts the (shipment, Unit Type) pairs with a known, non-zero delta.
public sealed record OrderTotalsDto(
    IReadOnlyList<UnitQuantityDto> Declared,
    IReadOnlyList<UnitQuantityDto> Actual,
    IReadOnlyList<UnitQuantityDto> OnStock,
    int QuantityDeltaCount,
    int PhotoCount,
    int CommentCount,
    int OutboundTrailerCount);
