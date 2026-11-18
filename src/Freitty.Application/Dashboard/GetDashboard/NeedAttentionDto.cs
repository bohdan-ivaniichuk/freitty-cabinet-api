namespace Freitty.Application.Dashboard.GetDashboard;

public sealed record NeedAttentionDto(
    int Total,
    int AwaitingAction,
    int Alerts,
    IReadOnlyList<AlertOrderDto> AlertOrders);
