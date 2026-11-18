using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.GetOrderDetail;

public sealed record AlertDto(Guid Id, AlertType Type, DateTimeOffset RaisedAt, DateTimeOffset? ResolvedAt);
