namespace Freitty.Domain.Orders;

// Plain data: Order.RecordOperation checks it as part of its calling contract.
public sealed record NewOperation(
    OperationType Type,
    UnitType UnitType,
    int Quantity,
    string? TrailerNumber,
    bool IsBillable,
    int PhotoCount,
    int CommentCount);
