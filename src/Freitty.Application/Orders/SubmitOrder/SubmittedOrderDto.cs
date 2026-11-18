using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.SubmitOrder;

// Number is the new, official one: the draft number no longer names the order.
public sealed record SubmittedOrderDto(string Number, OrderStatus Status, int Version);
