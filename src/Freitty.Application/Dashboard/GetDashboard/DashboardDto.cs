using Freitty.Application.Orders.Cards;

namespace Freitty.Application.Dashboard.GetDashboard;

public sealed record DashboardDto(
    ActiveOrdersDto ActiveOrders,
    CompletedOrdersDto Completed,
    NeedAttentionDto NeedAttention,
    IReadOnlyList<OrderCardDto> Orders,
    ActivityDto Activity);
