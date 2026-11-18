using Freitty.Domain.Orders;

namespace Freitty.Api.Endpoints.Orders.RecordOperation;

// The JSON body. The order number comes from the route; comments are not part of the request, as there is no chat,
// and photos are uploaded to the operation once it is recorded. The parameters with a default may be left out, which
// also tells the OpenAPI document so.
internal sealed record RecordOperationRequest(
    int ShipmentPosition,
    OperationType Type,
    UnitType UnitType,
    int Quantity,
    int ExpectedVersion,
    string? TrailerNumber = null,
    bool IsBillable = false);
