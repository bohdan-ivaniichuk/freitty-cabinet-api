using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.Cards;

// ScheduledAt, Destination and Transport are null in a draft that has not set them.
public sealed record OrderCardDto(
    string Number,
    OrderType Type,
    CrossDockKind? CrossDockKind,
    OrderStatus Status,
    IReadOnlyList<AlertType> OpenAlertTypes,
    UserDto CreatedBy,
    CardHubDto Hub,
    TransferHubDto? TransferHub,
    DateTimeOffset? ScheduledAt,
    DestinationDto? Destination,
    CardTransportDto? Transport,
    IReadOnlyList<UnitQuantityDto> Declared,
    int DeclaredTotal,
    int ActualTotal,
    bool HasQuantityDelta,
    int OutboundTrailers,
    IReadOnlyList<CardShipmentDto> Shipments,
    NextStepDto Next);
