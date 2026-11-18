using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.Cards;

// The quantities are sums over Unit Types.
public sealed record CardShipmentDto(
    string Label,
    string RefNumber,
    int DeclaredQuantity,
    int ActualQuantity,
    IReadOnlyList<AlertType> OpenAlertTypes);
