using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.ListPhotos;

// A photo with what the gallery groups it by: its shipment and its operation.
public sealed record PhotoDto(
    Guid Id,
    Guid OperationId,
    string ShipmentLabel,
    OperationType OperationType,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);
