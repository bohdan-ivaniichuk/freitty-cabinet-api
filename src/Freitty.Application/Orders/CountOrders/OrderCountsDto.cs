namespace Freitty.Application.Orders.CountOrders;

public sealed record OrderCountsDto(int All, int CrossDock, int Consolidation, int Alerts, int Drafts);
