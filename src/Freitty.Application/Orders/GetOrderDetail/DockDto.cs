namespace Freitty.Application.Orders.GetOrderDetail;

public sealed record DockDto(string Dock, string Bay, string FloorLead, DateTimeOffset AssignedAt);
