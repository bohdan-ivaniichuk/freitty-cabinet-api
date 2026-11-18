using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

// TrailerNumber is set for an Unloading and a Loading only.
public sealed record OperationDto(
    Guid Id,
    OperationType Type,
    UnitType UnitType,
    int Quantity,
    string? TrailerNumber,
    DateTimeOffset AppliedAt,
    bool IsBillable,
    int PhotoCount,
    int CommentCount);
