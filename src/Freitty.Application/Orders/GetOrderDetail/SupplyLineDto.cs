namespace Freitty.Application.Orders.GetOrderDetail;

// UnitPrice is the price the line was added at, not the catalogue's price today.
public sealed record SupplyLineDto(string Sku, string Category, int Quantity, decimal UnitPrice, decimal LineTotal);
