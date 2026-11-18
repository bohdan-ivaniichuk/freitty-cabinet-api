namespace Freitty.Application.Orders.GetOrderDetail;

public sealed record SuppliesDto(IReadOnlyList<SupplyLineDto> Lines, decimal Subtotal);
