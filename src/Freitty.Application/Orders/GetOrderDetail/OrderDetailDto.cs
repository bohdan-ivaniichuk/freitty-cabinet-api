using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.GetOrderDetail;

// ScheduledAt, Destination and Transport are null in a draft that has not set them. Version is what a write sends
// back to show which state of the order it was decided on.
public sealed record OrderDetailDto(
    string Number,
    OrderType Type,
    CrossDockKind? CrossDockKind,
    OrderStatus Status,
    OrderStage Stage,
    int Version,
    UserDto CreatedBy,
    DetailCompanyDto Company,
    DetailHubDto Hub,
    TransferHubDto? TransferHub,
    DateTimeOffset? ScheduledAt,
    DestinationDto? Destination,
    DetailTransportDto? Transport,
    IReadOnlyList<string> Services,
    OrderTotalsDto Totals,
    IReadOnlyList<DetailShipmentDto> Shipments,
    SuppliesDto Supplies,
    IReadOnlyList<InvoiceDto> Invoices);
