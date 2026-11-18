namespace Freitty.Domain.Orders;

// Plain data: Order.RecordOperation checks it as part of its calling contract. Photos are the ones taken with the
// operation; more can be attached later with Order.AttachPhotos.
public sealed record NewOperation(
    OperationType Type,
    UnitType UnitType,
    int Quantity,
    string? TrailerNumber,
    bool IsBillable,
    IReadOnlyList<NewPhoto> Photos,
    int CommentCount);
