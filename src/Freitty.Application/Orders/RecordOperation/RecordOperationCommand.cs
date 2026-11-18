using FastEndpoints;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.RecordOperation;

// The order is named by its number and the shipment by the position the page shows. ExpectedVersion is the version
// the caller saw: a stale page, a double click and a retry are refused instead of recorded twice.
public sealed record RecordOperationCommand(
    string Number,
    int ShipmentPosition,
    OperationType Type,
    UnitType UnitType,
    int Quantity,
    string? TrailerNumber,
    bool IsBillable,
    int PhotoCount,
    int ExpectedVersion)
    : ICommand<Result<RecordedOperationDto>>;
