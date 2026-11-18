using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.GetOrderDetail;

public sealed record ArrivalDto(
    TrailerType TrailerType,
    string TruckNumber,
    string TrailerNumber,
    DateTimeOffset ArrivedAt);
