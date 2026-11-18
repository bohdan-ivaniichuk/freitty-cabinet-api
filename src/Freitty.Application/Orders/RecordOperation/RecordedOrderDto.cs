using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.RecordOperation;

public sealed record RecordedOrderDto(string Number, OrderStatus Status, int Version);
