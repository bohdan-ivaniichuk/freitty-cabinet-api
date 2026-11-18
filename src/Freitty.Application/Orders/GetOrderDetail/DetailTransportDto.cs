using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.GetOrderDetail;

// The carrier's name and phone are set for a Carrier, DriverName for an own driver; self pickup has neither.
public sealed record DetailTransportDto(
    TransportKind Kind,
    string? CarrierName,
    string? CarrierPhone,
    string? DriverName);
