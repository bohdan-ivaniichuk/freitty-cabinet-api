using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.Cards;

// CarrierName is set for a Carrier, DriverName for an own driver; self pickup has neither.
public sealed record CardTransportDto(TransportKind Kind, string? CarrierName, string? DriverName);
