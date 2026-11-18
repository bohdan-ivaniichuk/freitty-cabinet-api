using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

public sealed record UnitQuantityDto(UnitType UnitType, int Quantity);
